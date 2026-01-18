import torch
from transformers import CLIPProcessor, CLIPModel
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from PIL import Image
import chromadb
import io
import uvicorn

# --- 1. Global Setup and Model Loading (Runs ONCE at startup) ---

# CUDA check (ensures maximum performance)
device = "cuda" if torch.cuda.is_available() else "cpu"
print(f"Initializing CLIP model on: {device}")

# Hugging Face CLIP model and processor
MODEL_ID = "openai/clip-vit-base-patch32" 
processor = CLIPProcessor.from_pretrained(MODEL_ID)
model = CLIPModel.from_pretrained(MODEL_ID).to(device)

# Local Persistent Vector Database Setup
DB_PATH = "./clip_vector_data" # Folder where embeddings are saved
chroma_client = chromadb.PersistentClient(path=DB_PATH)
# This collection holds the embeddings of the Art and Memes
COLLECTION = chroma_client.get_or_create_collection(name="persona_matches") 
print(f" ChromaDB initialized. Collection size: {COLLECTION.count()}")

app = FastAPI()

# --- 2. Core Vectorization Function (GPU Work) ---

def get_clip_embedding(image: Image.Image) -> list[float]:
    """Converts a PIL Image object into a CLIP vector (embedding)."""
    # 1. Preprocess: Resize, normalize, and convert to Tensor
    inputs = processor(images=image, return_tensors="pt").to(device)
    
    # 2. Inference: Generate vector on the GPU
    with torch.no_grad():
        embedding = model.get_image_features(pixel_values=inputs.pixel_values)
        
    # 3. Cleanup: Move to CPU and convert to a serializable list
    return embedding.cpu().numpy()[0].tolist()

# --- 3. Endpoints ---

@app.post("/index-image")
async def index_image(
    name: str = Form(...), 
    category: str = Form(...), 
    style: str = Form(...),  
    author: str = Form(...), 
    file: UploadFile = File(...)
):
    """
    ADMIN/SETUP Endpoint: Adds a new image (Art or Meme) to the searchable database.
    Input: Multipart form data (file, name, category).
    """
    try:
        image_bytes = await file.read()
        image = Image.open(io.BytesIO(image_bytes))
        
        # Core computation
        vector = get_clip_embedding(image)

        COLLECTION.add(
        embeddings=[vector],
        metadatas=[{
            "category": category, 
            "name": name, 
            "style": style,         
            "author": author,        
            "filename": file.filename
        }],
        ids=[name]
    )
        
        return {"status": "indexed", "id": name, "collection_size": COLLECTION.count()}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Indexing failed: {e}")


@app.post("/find-match")
async def find_match(file: UploadFile = File(...)):
    """
    USER Endpoint: Receives a selfie, finds the closest match in the database.
    Input: Multipart form data (file).
    """
    try:
        image_bytes = await file.read()
        image = Image.open(io.BytesIO(image_bytes))
        
        # 1. Compute vector for the user's photo
        query_vector = get_clip_embedding(image)
        
        # 2. Query the vector database (fast nearest neighbor search)
        results = COLLECTION.query(
            query_embeddings=[query_vector],
            n_results=1 # Only return the best match
        )
        
        if not results['ids'] or not results['ids'][0]:
             return {"status": "no_match", "message": "Database is empty or no close match found."}

        # 3. Format result
        match_id = results['ids'][0][0]
        distance = results['distances'][0][0]
        metadata = results['metadatas'][0][0]
        
        # NOTE: ChromaDB returns cosine distance. Lower is better. 0.0 is a perfect match.
        return {
            "status": "match_found",
            "match_id": match_id,
            "category": metadata['category'],
            "similarity_distance": distance,
            "metadata": metadata
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Match processing failed: {e}")

# --- 4. Running the Service ---

if __name__ == "__main__":
    # Ensure to run this from the terminal: 
    # uvicorn ai_service:app --host 0.0.0.0 --port 8000
    # For local testing, use:
    uvicorn.run(app, host="127.0.0.1", port=8000)