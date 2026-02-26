from unsloth import FastLanguageModel
from config import MODEL_NAME, MAX_SEQ_LENGTH

def load_model():
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=MODEL_NAME,
        max_seq_length=MAX_SEQ_LENGTH,
        load_in_4bit=True,
    )

    FastLanguageModel.for_inference(model)
    return model, tokenizer