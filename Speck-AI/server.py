# server.py

from fastapi import FastAPI
from threading import Lock
from pydantic import BaseModel
import torch

from model import load_model
from retriever import (
    retrieve,
    fetch_latest_cves
)
from router import classify_question
from prompts import (
    build_identity_prompt,
    build_prompt,
    build_general_prompt,
    build_nvd_prompt,
    build_cwe_prompt,
    build_latest_cve_prompt
)
from config import MAX_NEW_TOKENS, TEMPERATURE, DEVICE, MAX_SEQ_LENGTH

# Load model at startup
model, tokenizer = load_model()

app = FastAPI()

generation_lock = Lock()

class ChatRequest(BaseModel):
    message: str

@app.post("/chat")
def chat(req: ChatRequest):
    try:
        with generation_lock:
            user_question = req.message
            question_type = classify_question(user_question)

            if question_type == "identity":
                messages = build_identity_prompt(user_question)

            elif question_type == "kev":
                result = retrieve(user_question)

                if result:
                    if result["source"] == "kev":
                        messages = build_prompt(result["data"], user_question)

                    elif result["source"] == "nvd":
                        messages = build_nvd_prompt(result["data"], user_question)

                    elif result["source"] == "cwe":
                        messages = build_cwe_prompt(result["data"], user_question)

                else:
                    messages = build_general_prompt(user_question)

            elif question_type == "latest_cve":
                latest = fetch_latest_cves(days=1, limit=5)
                messages = build_latest_cve_prompt(latest, user_question)

            elif question_type == "latest_cwe":
                messages = build_general_prompt(
                    "CWE taxonomy does not publish daily entries like CVEs. "
                    "Ask about a specific CWE ID."
                )

            else:
                messages = build_general_prompt(user_question)

            text = tokenizer.apply_chat_template(
                messages,
                tokenize=False,
                add_generation_prompt=True
            )

            inputs = tokenizer(
                text,
                return_tensors="pt",
                truncation=True,
                max_length=MAX_SEQ_LENGTH
            ).to(DEVICE)

            with torch.no_grad():
                outputs = model.generate(
                    **inputs,
                    max_new_tokens=MAX_NEW_TOKENS,
                    temperature=TEMPERATURE,
                    do_sample=False,
                )

            response = tokenizer.decode(
                outputs[0][inputs["input_ids"].shape[-1]:],
                skip_special_tokens=True
            )

            return {"response": response}
    except Exception as e:
        print("SERVER ERROR:", e)
        return {"response": f"Server error: {str(e)}"}