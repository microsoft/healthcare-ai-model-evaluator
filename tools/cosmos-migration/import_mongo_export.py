#!/usr/bin/env python3

import argparse
import json
import sys
from datetime import datetime, timezone
from typing import Any, Dict, Iterable, List, Optional

from azure.identity import DefaultAzureCredential
from azure.cosmos import CosmosClient


def _iter_documents(path: str) -> Iterable[Dict[str, Any]]:
    """Supports either a JSON array file or newline-delimited JSON (NDJSON)."""
    with open(path, "r", encoding="utf-8") as f:
        content = f.read().strip()
        if not content:
            return []

        if content.startswith("["):
            data = json.loads(content)
            if not isinstance(data, list):
                raise ValueError("Expected a JSON array")
            return data

    # NDJSON fallback
    docs: List[Dict[str, Any]] = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            docs.append(json.loads(line))
    return docs


def _ms_epoch_to_iso(ms_epoch: int) -> str:
    dt = datetime.fromtimestamp(ms_epoch / 1000.0, tz=timezone.utc)
    return dt.isoformat()


def _normalize_extended_json(value: Any) -> Any:
    """Convert Mongo Extended JSON (mongoexport) into plain JSON values.

    Examples:
    - {"$oid": "..."} -> "..."
    - {"$date": "2020-01-01T00:00:00Z"} -> "2020-01-01T00:00:00Z"
    - {"$date": {"$numberLong": "1700000000000"}} -> ISO string
    - {"$numberLong": "123"} -> 123
    """

    if isinstance(value, list):
        return [_normalize_extended_json(v) for v in value]

    if not isinstance(value, dict):
        return value

    if len(value) == 1:
        key, inner = next(iter(value.items()))

        if key == "$oid":
            return str(inner)

        if key == "$uuid":
            return str(inner)

        if key in ("$numberInt", "$numberLong"):
            return int(inner)

        if key in ("$numberDouble", "$numberDecimal"):
            return float(inner)

        if key == "$date":
            if isinstance(inner, str):
                return inner
            if isinstance(inner, (int, float)):
                return _ms_epoch_to_iso(int(inner))
            if isinstance(inner, dict) and "$numberLong" in inner:
                return _ms_epoch_to_iso(int(inner["$numberLong"]))
            return inner

    return {k: _normalize_extended_json(v) for k, v in value.items()}


def _extract_mongo_id(value: Any) -> Optional[str]:
    if value is None:
        return None

    # Common mongoexport format: {"$oid":"..."}
    if isinstance(value, dict) and "$oid" in value:
        oid = value.get("$oid")
        return str(oid) if oid is not None else None

    # Sometimes mongoexport emits string IDs
    if isinstance(value, str):
        return value

    # As a last resort, stringify
    return str(value)


def _normalize_document(doc: Dict[str, Any]) -> Dict[str, Any]:
    doc = _normalize_extended_json(doc)

    # Map MongoDB _id -> Cosmos SQL id
    if "id" not in doc or not doc.get("id"):
        mongo_id = _extract_mongo_id(doc.get("_id"))
        if mongo_id:
            doc["id"] = mongo_id

    if "_id" in doc:
        doc.pop("_id", None)

    if "id" not in doc or not doc.get("id"):
        raise ValueError("Document is missing an id after normalization")

    # Cosmos SQL requires id to be a string. This deployment partitions on /id.
    doc["id"] = str(doc["id"])

    return doc


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Import a mongoexport JSON dump (Cosmos Mongo API) into Cosmos DB SQL API. "
            "This script maps _id -> id and upserts documents into the target container."
        )
    )
    parser.add_argument("--endpoint", required=True, help="Cosmos DB account endpoint (e.g. https://<acct>.documents.azure.com:443/)")
    parser.add_argument("--database", required=True, help="Cosmos DB database name")
    parser.add_argument("--container", required=True, help="Cosmos DB container name")
    parser.add_argument("--input", required=True, help="Path to mongoexport JSON file (JSON array or NDJSON)")
    parser.add_argument("--dry-run", action="store_true", help="Validate and print counts without writing")

    args = parser.parse_args()

    credential = DefaultAzureCredential()
    client = CosmosClient(url=args.endpoint, credential=credential)
    container = client.get_database_client(args.database).get_container_client(args.container)

    docs = list(_iter_documents(args.input))
    if not docs:
        print("No documents found in input.")
        return 0

    normalized: List[Dict[str, Any]] = []
    for raw in docs:
        if not isinstance(raw, dict):
            raise ValueError("Each document must be a JSON object")
        normalized.append(_normalize_document(raw))

    if args.dry_run:
        print(f"Validated {len(normalized)} documents (dry run).")
        return 0

    upserted = 0
    for doc in normalized:
        # Partition key is assumed to be /id
        container.upsert_item(doc)
        upserted += 1

    print(f"Upserted {upserted} documents into {args.database}/{args.container}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        raise
