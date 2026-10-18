# Architecture Decision Records

ADR เก็บเหตุผลของการตัดสินใจสำคัญ พร้อมทางเลือกและผลที่ยอมรับ หนึ่งเอกสารตอบหนึ่งการตัดสินใจ

| ADR | สถานะ | หัวข้อ |
|---|---|---|
| [ADR-0001](0001-api-enforced-team-isolation.md) | Proposed | บังคับ Team Isolation ที่ ASP.NET Core API |

ADR-0001 เป็นตัวอย่างจาก source จริงของ Team Notes สำหรับหัวข้อ Secure Design & Threat Modeling ในคอร์ส SonarQube Developer Training เป็นข้อเสนอปรับปรุงแอปฝึกที่มีช่องโหว่ ไม่ใช่หลักฐานว่า code ถูกแก้หรือผ่าน security tests แล้ว

สถานะหลัก: Proposed → Accepted / Rejected; หากการตัดสินใจที่ยอมรับแล้วถูกแทน ให้คง ADR เดิมและเชื่อมไป ADR ใหม่ด้วย Superseded. แยกสถานะการตัดสินใจออกจากสถานะ implementation เสมอ
