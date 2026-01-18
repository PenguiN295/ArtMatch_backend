import requests
import os
from io import BytesIO

# --- Configuration ---
AI_SERVICE_URL = "http://127.0.0.1:8000"
DATA_ROOT = "D:/DataSets" 

def index_image_file(file_path: str, category: str, name: str, style: str, author: str):
    """Sends a single image and its rich metadata to the FastAPI index endpoint."""
    with open(file_path, 'rb') as f:
        files = {
            'file': (os.path.basename(file_path), f.read(), 'image/jpeg')
        }
    
    data = {
        'name': name,
        'category': category, # e.g., 'art'
        'style': style,       # e.g., 'Cubism'
        'author': author      # e.g., 'amadeo-de-souza-cardoso'
    }

    # 2. Send Request
    try:
        response = requests.post(f"{AI_SERVICE_URL}/index-image", data=data, files=files)
        response.raise_for_status() 
        
        print(f"  -> SUCCESS: {name} (Size: {response.json().get('collection_size')})")
        return response.json()
        
    except requests.exceptions.RequestException as e:
        print(f"  -> ERROR indexing {name}: {e}")
        return None


def bulk_index_data():
    print(f"Starting bulk indexing from: {DATA_ROOT}")
    
    indexed_count = 0
    
    for root_dir, dirs, files in os.walk(DATA_ROOT):
        path_parts = os.path.normpath(root_dir).split(os.sep)
        
        if len(path_parts) < 3:
            continue 
        style_or_author = path_parts[-1] 
        

        primary_category = path_parts[-2] 

        for file_name in files:
            if file_name.lower().endswith(('.png', '.jpg', '.jpeg')):
                file_path = os.path.join(root_dir, file_name)
                
                #Filename Parsing (Author and Picture Name)
                base_name = os.path.splitext(file_name)[0]
                
                if '_' in base_name:
                    author_name, picture_name = base_name.split('_', 1)
                else:
                    author_name = style_or_author # Fallback: use folder name as author
                    picture_name = base_name

                #Create unique ID and format names
                unique_id = f"{primary_category}_{author_name}_{picture_name}"
                
                print(f"Indexing: {file_name}. Cat='{primary_category}', Style/Author='{style_or_author}'")
                
                result = index_image_file(
                    file_path=file_path, 
                    category=primary_category, 
                    name=unique_id, 
                    style=style_or_author, 
                    author=author_name
                )
                if result:
                    indexed_count += 1

    print(f"--- Indexing Complete. Total images indexed: {indexed_count} ---")


if __name__ == "__main__":
    bulk_index_data()