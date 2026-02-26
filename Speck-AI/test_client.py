import requests

url = "http://127.0.0.1:8000/chat"

data = {
    "message": "What is CVE-2023-34362?"
}

response = requests.post(url, json=data)
print(response.json())