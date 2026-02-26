def build_identity_prompt(user_question):
    system_prompt = (
        "You are Speck, a cybersecurity chicken assistant."
        "Explain in a friendly tone with occasional (*Pock* *Pock*)."
    )

    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_question}
    ]

    return messages

def build_prompt(example, user_question):
    system_prompt = ( "You are Speck, a cybersecurity chicken with a deep knowledge about vulnerabilities and how to mitigate them. "
        "You analyze vulnerabilities with deep technical knowledge while maintaining a friendly, approachable tone. "
        "Explain with occasional chicken sounds (*Pock* *Pock*). "
        "Explain step by step."
        "If the question cannot be answered using the KEV entry, respond: 'Not found in KEV.'"
        )

    context_block = f"""
      CVE ID: {example.get("cveID", "N/A")}
      Vendor/Project: {example.get("vendorProject", "N/A")}
      Product: {example.get("product", "N/A")}
      Vulnerability Name: {example.get("vulnerabilityName", "N/A")}
      Date Added: {example.get("dateAdded", "N/A")}
      Short Description: {example.get("shortDescription", "N/A")}
      Required Action: {example.get("requiredAction", "N/A")}
      Due Date: {example.get("dueDate", "N/A")}
      Known Ransomware Use: {example.get("knownRansomwareCampaignUse", "N/A")}
      CWEs: {", ".join(example.get("cwes", [])) if example.get("cwes") else "N/A"}
      Notes: {example.get("notes", "N/A")}
    """

    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": f"""
Use ONLY the following KEV entry to answer.

{context_block}

Question: {user_question}
"""}
    ]

    return messages

def build_general_prompt(user_question):
    system_prompt = (
        "You are Speck, a cybersecurity chicken assistant. "
        "You can answer general cybersecurity and technical questions. "
        "Be accurate and explain step by step with occasional (*Pock* *Pock*)."
    )

    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_question}
    ]

    return messages

def build_nvd_prompt(nvd_data, user_question):
    system_prompt = (
        "You are Speck, a cybersecurity chicken with deep vulnerability knowledge. "
        "Explain clearly step-by-step with occasional (*Pock* *Pock*)."
    )

    context_block = f"""
CVE ID: {nvd_data.get("cveID")}
Description: {nvd_data.get("description")}
CVSS Score: {nvd_data.get("cvss_score")}
Attack Vector: {nvd_data.get("attack_vector")}
"""

    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": f"""
Use ONLY the following CVE intelligence:

{context_block}

Question: {user_question}
"""}
    ]

    return messages

def build_cwe_prompt(cwe_data, user_question):
    system_prompt = (
        "You are Speck, a cybersecurity chicken specializing in weakness analysis. "
        "Explain root causes, exploitation mechanics, and mitigations step-by-step "
        "with occasional (*Pock* *Pock*)."
    )

    context_block = f"""
CWE ID: {cwe_data.get("id")}
CWE Name: {cwe_data.get("name")}
CWE Description: {cwe_data.get("description")}
"""

    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": f"""
Use ONLY the following CWE definition:

{context_block}

Question: {user_question}
"""}
    ]

    return messages

def build_latest_cve_prompt(cve_list, user_question):
    system_prompt = (
        "You are Speck, a cybersecurity chicken assistant. "
        "Summarize recent CVEs clearly with technical accuracy "
        "and occasional (*Pock* *Pock*)."
    )

    context = ""
    for c in cve_list:
        context += f"\nCVE ID: {c['id']}\nDescription: {c['description']}\n"

    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": f"""
Here are the most recently published CVEs:

{context}

Question: {user_question}
"""}
    ]

    return messages