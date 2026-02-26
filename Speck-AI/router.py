import re

def classify_question(question: str):
    q = question.lower()

    # Identity / meta questions
    identity_keywords = [
        "who are you",
        "what are you",
        "what model",
        "what do you do",
        "who made you"
    ]
    if any(k in q for k in identity_keywords):
        return "identity"
    
    if re.search(r"cwe[- ]?\d+", q):
        return "kev"

    # Direct CVE mention
    if re.search(r"cve-\d{4}-\d+", q):
        return "kev"
    
    if any(k in q for k in ["latest cve", "newest cve", "recent cve"]):
        return "latest_cve"

    if any(k in q for k in ["latest cwe", "newest cwe"]):
        return "latest_cwe"

    return "general"