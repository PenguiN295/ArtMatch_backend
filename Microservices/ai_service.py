import numpy as np
import torch
from transformers import CLIPProcessor, CLIPModel
from fastapi import FastAPI, Response, UploadFile, File, Form, HTTPException
from PIL import Image
import chromadb
import io
import uvicorn
import insightface
from insightface.app import FaceAnalysis
import cv2
import os
import onnxruntime as ort




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


app_face = FaceAnalysis(name='buffalo_l')
app_face.prepare(ctx_id=0, det_size=(640, 640))

swapper = insightface.model_zoo.get_model('inswapper_128.onnx', download=False, set_params=None)

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
    


@app.post("/swap-face")
async def swap_face(
    target_path: str = Form(...),
    file: UploadFile = File(...)
):
    # 1. Load User Photo (Source)
    user_bytes = await file.read()
    nparr = np.frombuffer(user_bytes, np.uint8)
    user_img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    
    # 2. Get User Face
    user_faces = app_face.get(user_img)
    if not user_faces:
        raise HTTPException(status_code=400, detail="No face detected in your photo.")
    source_face = user_faces[0] # Take the largest/first face

    # 3. Load Target Art (From MatchID)
    target_img = cv2.imread(target_path)

    # 4. Get Target Face (The face in the painting)
    target_faces = app_face.get(target_img)
    if not target_faces:
        raise HTTPException(status_code=400, detail="No face detected in the art piece.")
    target_face = target_faces[0]

    # 5. Perform the Swap
    # Result is a standard OpenCV image (numpy array)
    result_img = swapper.get(target_img, target_face, source_face, paste_back=True)

    # 6. Return Image
    _, encoded_img = cv2.imencode('.jpg', result_img)
    return Response(content=encoded_img.tobytes(), media_type="image/jpeg")

@app.post("/check-face")
async def check_face(file: UploadFile = File(...)):
    img_bytes = await file.read()
    img = cv2.imdecode(np.frombuffer(img_bytes, np.uint8), cv2.IMREAD_COLOR)
    faces = app_face.get(img)
    if(len(faces)>0):
        return True
    return False


if __name__ == "__main__":
    # For local testing, use:
    uvicorn.run(app, host="127.0.0.1", port=8000)