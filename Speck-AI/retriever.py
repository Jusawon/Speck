# retriever.py

import requests
import re
from config import URL
from datetime import datetime, timedelta

# -------------------------
# Internal cache
# -------------------------
kev_data = []
cve_map = {}

def fetch_kev():
    try:
        response = requests.get(URL, timeout=10)
        response.raise_for_status()
        data = response.json().get("vulnerabilities", [])
        return data
    except Exception as e:
        print(f"[KEV FETCH ERROR] {e}")
        return []


def initialize_kev():
    global kev_data, cve_map
    kev_data = fetch_kev()
    cve_map = {row["cveID"].lower(): row for row in kev_data}


# Initialize at startup
initialize_kev()

def row_to_text(row):
    return f"""
CVE ID: {row.get("cveID")}
Vendor/Project: {row.get("vendorProject")}
Product: {row.get("product")}
Vulnerability Name: {row.get("vulnerabilityName")}
Date Added: {row.get("dateAdded")}
Short Description: {row.get("shortDescription")}
Required Action: {row.get("requiredAction")}
Due Date: {row.get("dueDate")}
Known Ransomware Use: {row.get("knownRansomwareCampaignUse")}
CWEs: {", ".join(row.get("cwes", []))}
Notes: {row.get("notes")}
""".strip()

kev_data = fetch_kev()

cve_map = {row["cveID"].lower(): row for row in kev_data}

def retrieve(query: str):
    q = query.lower()

    # 1️⃣ Direct CWE match (handle first)
    cwe_match = re.search(r"cwe[- ]?(\d+)", q)
    if cwe_match:
        cwe_id = f"CWE-{cwe_match.group(1)}"
        cwe_data = fetch_cwe_details(cwe_id)
        if cwe_data:
            return {"source": "cwe", "data": cwe_data}
        return None

    # 2️⃣ Direct CVE match
    cve_match = re.search(r"cve-\d{4}-\d+", q)
    if cve_match:
        cve_id = cve_match.group(0).lower()

        # KEV first
        kev_entry = cve_map.get(cve_id)
        if kev_entry:
            return {"source": "kev", "data": kev_entry}

        # Fallback to NVD
        nvd_entry = fetch_nvd(cve_id)
        if nvd_entry:
            return {"source": "nvd", "data": nvd_entry}

        return None

    return None

def fetch_nvd(cve_id: str):
    url = f"https://services.nvd.nist.gov/rest/json/cves/2.0?cveId={cve_id.upper()}"
    try:
        r = requests.get(url, timeout=10)
        r.raise_for_status()
        data = r.json()

        vulnerabilities = data.get("vulnerabilities", [])
        if not vulnerabilities:
            return None

        cve = vulnerabilities[0]["cve"]

        description = cve["descriptions"][0]["value"]

        metrics = cve.get("metrics", {})
        cvss_score = None
        attack_vector = None

        if "cvssMetricV31" in metrics:
            metric = metrics["cvssMetricV31"][0]
            cvss_score = metric["cvssData"]["baseScore"]
            attack_vector = metric["cvssData"].get("attackVector")

        weaknesses = cve.get("weaknesses", [])
        cwe_id = None
        if weaknesses:
            cwe_id = weaknesses[0]["description"][0]["value"]

        return {
            "cveID": cve_id.upper(),
            "description": description,
            "cvss_score": cvss_score,
            "attack_vector": attack_vector,
            "cwe": cwe_id
        }

    except Exception as e:
        print(f"[NVD FETCH ERROR] {e}")
        return None

def fetch_cwe_details(cwe_id: str):
    try:
        cwe_number = cwe_id.replace("CWE-", "")
        url = f"https://cwe-api.mitre.org/api/v1/cwe/{cwe_number}"
        r = requests.get(url, timeout=10)

        if r.status_code != 200:
            return None

        data = r.json()

        name = data.get("Name")
        description = data.get("Description")

        return {
            "id": cwe_id,
            "name": name,
            "description": description
        }

    except Exception as e:
        print(f"[CWE FETCH ERROR] {e}")
        return None
    
def fetch_latest_cves(days=1, limit=5):
    end = datetime.utcnow()
    start = end - timedelta(days=days)

    start_str = start.strftime("%Y-%m-%dT%H:%M:%S.000")
    end_str = end.strftime("%Y-%m-%dT%H:%M:%S.000")

    url = (
        "https://services.nvd.nist.gov/rest/json/cves/2.0"
        f"?pubStartDate={start_str}"
        f"&pubEndDate={end_str}"
    )

    try:
        r = requests.get(url, timeout=10)
        r.raise_for_status()
        data = r.json()

        vulns = data.get("vulnerabilities", [])[:limit]

        results = []
        for v in vulns:
            cve = v["cve"]
            results.append({
                "id": cve["id"],
                "description": cve["descriptions"][0]["value"]
            })

        return results

    except Exception as e:
        print("[LATEST CVE ERROR]", e)
        return []