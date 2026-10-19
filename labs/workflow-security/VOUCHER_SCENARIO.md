# Voucher: โจทย์ออกแบบคูปองใช้สิทธิ์ครั้งเดียว

## เรากำลังออกแบบอะไร

สมมติทีมต้องการเพิ่มฟีเจอร์ให้ Team Notes แจก **คูปองรับสิทธิ์เข้าร่วมกิจกรรมภายในหนึ่งครั้ง** แต่ละคูปองมีรหัสและวันหมดอายุ ผู้ถือรหัสขอใช้สิทธิ์ได้หลังเปิดใช้งานแล้ว เมื่อใช้สำเร็จต้องมีหลักฐานรับสิทธิ์หนึ่งรายการ และใช้คูปองเดิมซ้ำไม่ได้

นี่คือ **ฟีเจอร์สมมติที่ต้องออกแบบ (design proposal)** ไม่ใช่ฟีเจอร์ที่ Team Notes มีอยู่แล้ว ไม่มีหน้าจอ Voucher, HTTP endpoint, ตารางฐานข้อมูล หรือระบบชำระเงินจริงให้เรียกใช้ในโจทย์นี้ การรับสิทธิ์แทนด้วย receipt และ business event จำลอง

## ใครทำอะไร: สมมติฐานตั้งต้นสำหรับออกแบบ

| ผู้เกี่ยวข้องที่เสนอ | Action | ความหมาย |
| --- | --- | --- |
| ผู้จัดกิจกรรม | `issue` | สร้างรหัสคูปองและกำหนดเวลาหมดอายุ เริ่มที่ `issued` |
| ผู้จัดกิจกรรม | `activate` | เปิดให้ใช้คูปองที่ยังเป็น `issued` เปลี่ยนเป็น `active` |
| สมาชิกที่ได้รับรหัส | `redeem` | ขอใช้สิทธิ์หนึ่งครั้ง ถ้าผ่านกติกาให้ receipt/event และเปลี่ยนเป็น `used` |
| ผู้จัดกิจกรรม | `cancel` | ยกเลิกคูปองที่เป็น `issued` หรือ `active` เปลี่ยนเป็น `cancelled` |

บทบาทในตารางเป็น **สมมติฐานสำหรับ ADR** ผู้เรียนต้องเลือกและอธิบายสิทธิ์ของแต่ละ action เอง ไม่ใช่ authorization ที่ fixture บังคับแล้ว การผูกคูปองกับผู้ใช้หรือทีม การโอนรหัส และสิทธิ์ admin ยังเป็นคำถามออกแบบ ห้ามอนุมานจากการมี `actor` หรือจาก team-isolation ADR ตัวอย่าง

## กติกาที่ต้องรักษา

- ต้อง `activate` ก่อน `redeem` และรับใช้สิทธิ์ได้เฉพาะ `active` ที่ `now < expires_at`
- รหัสไม่รู้จักหรือสถานะ `issued`, `cancelled`, `expired`, `used` ต้องถูกปฏิเสธโดยไม่สร้าง receipt/redemption/event ใหม่
- ที่ `now == expires_at` ถือว่าหมดอายุแล้ว จะคงสถานะ `active` หรือเปลี่ยนเป็น `expired` เพื่อบันทึกสถานะก็ได้ แต่ห้ามขยายเวลาเพื่อให้ใช้สิทธิ์ต่อ
- คูปองหนึ่งใบสำเร็จได้ไม่เกินหนึ่งครั้งและสร้าง business event ได้ไม่เกินหนึ่งรายการ แม้ผู้ใช้เดิมส่งซ้ำหรือมีสองคำขอพร้อมกัน
- โจทย์นี้ปฏิเสธการใช้ซ้ำ ไม่ใช้การส่ง receipt เดิมกลับมาเป็นความสำเร็จซ้ำ สัญญาแบบ idempotent response เป็นอีกทางเลือกที่ต้องกำหนดต่างหาก

## ข้อมูลที่ fixture มีจริง

อ่าน [Voucher/VoucherModel.cs](Voucher/VoucherModel.cs) และ [Business contract ใน README](README.md#business-contract) เพื่อแยกแบบจำลองที่มีจริงออกจากระบบที่เสนอ

| ข้อมูล | ความหมายในแบบจำลอง |
| --- | --- |
| `Voucher.Code` | รหัสคูปองที่ใช้ค้นหารายการ |
| `Voucher.ExpiresAt` | เวลาเส้นตายแบบจำนวนเต็ม ไม่ใช่ timezone/ระบบนาฬิกา production |
| `Voucher.State` | `issued`, `active`, `cancelled`, `expired` หรือ `used` |
| `Voucher.RedeemedBy` | ค่า actor เมื่อใช้สิทธิ์สำเร็จ ไม่ใช่หลักฐานว่า actor ผ่าน authentication |
| `Redemption` | ผลลัพธ์ `Ok`, `Reason`, `ReceiptId`, `Actor` (ใน JSON ของ probes คือ `ok`, `reason`, `receipt_id`, `actor`) |
| `Snapshot().RedemptionCount` และ `Events()` | จำนวนผลสำเร็จที่คำนวณจาก events และรายการ effects จำลอง |

ภายหลังจะใช้ `VoucherService` ใน [Voucher/VoucherService.cs](Voucher/VoucherService.cs) (C# / .NET 10) เป็น **single-process in-memory fixture ที่จงใจมีช่องโหว่** สำหรับสังเกตว่าพฤติกรรมต่างจากกติกาข้างต้นอย่างไร อย่าใช้ implementation ตั้งต้นเป็นคำตอบที่ถูกต้องของ contract

`actor` เป็นสตริงจำลอง ส่วน `now` เป็นนาฬิกาทดสอบที่ควบคุมได้ เช่น 10 และเส้นตาย 100 ค่า `checkpoint` เป็นจุดนัดหมายของ test เพื่อสร้าง concurrency ไม่ใช่ input ของผู้ใช้ในระบบจริง ไม่มี HTTP, authentication, durable database หรือ distributed transaction ที่ fixture พิสูจน์ให้แล้ว

## สิ่งที่นำไปเขียน ADR

1. สรุปปัญหาและขอบเขตฟีเจอร์ พร้อม action inventory: ผู้เรียก ข้อมูลที่อ่าน/เขียน และสิทธิ์ที่เสนอสำหรับ `issue`, `activate`, `redeem`, `cancel`
2. วาด context/container ของระบบที่เสนอ ติดป้ายส่วนที่ยังไม่ implement และระบุ trust boundary ระหว่างผู้ใช้, UI/API, VoucherService และ storage
3. เขียนสอง quality scenarios เช่น คำขอพร้อมกันต้องให้ผลสำเร็จไม่เกินหนึ่งครั้ง และความล้มเหลวระหว่างเปลี่ยน state กับเขียน event ต้องไม่สร้างผลซ้ำ
4. เปรียบเทียบอย่างน้อยสองทางเลือก เช่น modular monolith กับ service แยก เลือก decision พร้อมเหตุผล trade-offs, constraints, assumptions และเงื่อนไขทบทวน สถานะเริ่มต้น `Proposed`

ใช้ [ADR-0001 ของ Team Notes](../../docs/adr/0001-api-enforced-team-isolation.md) เป็นตัวอย่างโครงเอกสารและการแยกข้อเสนอออกจากผล implementation เท่านั้น ไม่คัดลอก team-isolation policy มาเป็น voucher policy โดยอัตโนมัติ นำ ADR และ action inventory ไปใช้กับ DFD/STRIDE แล้วค่อยกำหนด control contract ก่อนทดลองกับ fixture

## งานออกแบบที่ต่อจาก ADR

ใช้โฟลเดอร์ผลงานแยกกันสำหรับ ADR, DFD และ controls เพื่อไม่เขียนทับ `result.md`/`result.html` ของงานก่อนหน้า ไฟล์เหล่านี้เป็นเอกสารที่ผู้เรียนสร้าง ไม่ใช่ source code ของแอป

- **DFD:** ส่ง `diagram.svg`, `result.md` และ `result.html` จาก ADR เดิม ให้มี actor, flow IDs, ข้อมูลเข้า/ออก, trust boundaries และเส้นทาง UI/API → VoucherService → records/events → response ติดป้ายส่วนที่เสนอแยกจาก fixture แล้วเลือก threats สองข้อ (เช่น T1/T2) พร้อม STRIDE category, preconditions, impact และ missing evidence
- **Controls:** ใช้ DFD/T1/T2 ทำ `VoucherControlContract.md`, `result.md` และ `result.html` เชื่อม abuse → control → expected behavior ของ state/time/replay/atomicity มีทั้ง negative และ positive predicates พร้อม risk ledger สองรายการที่ระบุ impact/likelihood, priority, owner, response และ residual risk
- **Handoff:** ส่ง ADR → DFD → control contract ชุดเดียวกันไปใช้สังเกต fixture ในกิจกรรมถัดไป การออกแบบยังไม่ใช่ผลรันหรือการติดตั้ง controls จริง ข้อกำหนด HTTP/auth/DB/distributed ที่ fixture ไม่รองรับต้องคงเป็นช่องว่างที่ยังไม่ทดสอบ
