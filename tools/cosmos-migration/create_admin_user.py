#!/usr/bin/env python3

import argparse
import sys
import uuid
from datetime import datetime, timezone

from azure.cosmos import CosmosClient
from azure.identity import DefaultAzureCredential


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def main() -> int:
    parser = argparse.ArgumentParser(description="Create (upsert) the first Administrator user in Cosmos DB SQL API.")
    parser.add_argument("--endpoint", required=True, help="Cosmos DB account endpoint")
    parser.add_argument("--database", required=True, help="Cosmos DB database name")
    parser.add_argument("--email", required=True, help="Admin email")
    parser.add_argument("--name", required=True, help="Admin display name")
    args = parser.parse_args()

    email = args.email.strip().lower()
    name = args.name.strip()

    if not email or "@" not in email:
        raise ValueError("--email must be a valid email")
    if not name:
        raise ValueError("--name is required")

    user_doc = {
        "id": uuid.uuid4().hex,
        "Name": name,
        "Email": email,
        "Roles": ["Administrator"],
        "CreatedAt": _utc_now_iso(),
        "UpdatedAt": _utc_now_iso(),
        "IsModelReviewer": False,
        "ModelId": None,
        "Expertise": None,
        "Stats": {},
        "PasswordHash": None,
        "PasswordSalt": None,
        "PasswordResetToken": None,
        "PasswordResetExpires": None,
    }

    credential = DefaultAzureCredential()
    client = CosmosClient(url=args.endpoint, credential=credential)
    container = client.get_database_client(args.database).get_container_client("Users")

    container.upsert_item(user_doc)
    print(f"Upserted administrator user for {email}.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        raise
