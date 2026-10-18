# ADR-0001: บังคับ Team Isolation ที่ ASP.NET Core API

- **Status:** Proposed — ตัวอย่างข้อเสนอเพื่อการเรียน ยังไม่ใช่การตัดสินใจที่ทีมอนุมัติหรือ implementation ที่เสร็จแล้ว
- **Date:** 2026-10-13
- **Project:** `sonarqube-training-team-notes`
- **Source snapshot:** branch `main`, commit `158536e` (`158536e` เป็นรหัสย่อของ commit ที่ตรวจ)
- **Decision owner / reviewers:** เสนอให้ผู้ดูแล API และผู้ทบทวนด้าน Security พิจารณา; ยังไม่ได้รับการอนุมัติ

## Context

Team Notes เป็นแอปฝึกที่มีช่องโหว่โดยตั้งใจ โครงสร้างปัจจุบันมี Next.js web, ASP.NET Core API และ PostgreSQL แยก container แต่ใช้ฐานข้อมูลร่วมกันตาม `docker-compose.yml`. ผู้ใช้และโน้ตมี `team_id`; โน้ตมี `owner_id` ด้วย (`db/init.sql`). Next.js ส่ง request ไปยัง API; ผู้ใช้จึงเรียก API โดยตรงได้โดยไม่ผ่านหน้าจอเว็บ

ใน `api/Endpoints/NotesEndpoints.cs`, `ListAsync` กรองด้วย `session.TeamId` แล้ว แต่ `GetAsync` และ `DeleteAsync` ใช้ note ID โดยไม่ตรวจทีม; `UpdateAsync` รับ field จากผู้เรียกผ่าน `Mutations.UpdateRowAsync`. ส่วน `SearchAsync` ต่อค่า input เข้า SQL string จึงยังมีช่องโหว่ SQL injection แม้จะมีเงื่อนไข `team_id` อยู่ในข้อความ query

นอกจากนี้ `api/Auth.cs` ยังปิดการตรวจ JWT signature และ lifetime ใน snapshot นี้ การมี object ชื่อ `Session` จึงไม่ใช่หลักฐานว่า identity เชื่อถือได้แล้ว ข้อเสนอด้าน authorization ต้องมีเงื่อนไขก่อนใช้งานคือแก้และทดสอบการตรวจ credentials ให้ถูกต้อง

คำถามที่ ADR นี้ตอบคือ **ใครเป็นผู้บังคับสิทธิ์การเข้าถึงข้อมูลทีม เมื่อระบบยังใช้ API และฐานข้อมูลร่วมกัน?** เอกสารนี้ไม่เปลี่ยน deployment ไม่ออกแบบระบบ Voucher และไม่อ้างว่าการเขียน ADR ทำให้ baseline ปลอดภัยขึ้นทันที

## Decision drivers

1. สมาชิกของทีมหนึ่งต้องอ่านหรือเปลี่ยนโน้ตของอีกทีมไม่ได้ แม้รู้ note ID หรือส่ง HTTP เอง
2. ทุกช่องทางเข้าถึงข้อมูลต้องใช้กติกาเดียวกัน ทั้ง Next.js, API client และ request ที่ส่งตรง
3. ทีมต้องแก้และตรวจได้ทีละ endpoint โดยไม่ต้องแยกฐานข้อมูลหรือบริการใหม่ทันที
4. บทบาทและสิทธิ์พิเศษต้องชัดเจน: การเป็น admin ไม่ได้ให้สิทธิ์ข้ามทีมโดยปริยาย และ public share เป็นอีกช่องทางสิทธิ์หนึ่ง

## Options considered

| ทางเลือก | ข้อดี | ต้นทุน / ข้อจำกัด | ผลการพิจารณา |
|---|---|---|---|
| ตรวจสิทธิ์เฉพาะ Next.js / ซ่อนปุ่ม | เปลี่ยนหน้าจอได้น้อยจุด | request ตรงไป API ข้ามข้อจำกัดหน้าจอได้ | ไม่เลือกเป็น security boundary |
| ให้ ASP.NET Core API บังคับ policy และ query scope | ครอบคลุมทุก client ใช้โครงสร้างเดิม และทดสอบ negative/positive case ได้ | ต้องตรวจครบทุกเส้นทางและป้องกัน policy drift | **เสนอเลือกใน ADR นี้** |
| เพิ่ม PostgreSQL RLS ควบคู่กับ API policy | เพิ่มการป้องกันที่ชั้นข้อมูลเมื่อ query ลืมกรองทีม | ต้องออกแบบ DB roles, transaction-scoped tenant context, pooling และ migration ให้ถูกต้อง | พิจารณาใน ADR ถัดไปเมื่อมีเหตุผลรองรับ; ไม่ใช้แทน API authorization |

## Decision

เสนอให้ **ASP.NET Core API เป็นจุดบังคับสิทธิ์การเข้าถึงข้อมูลทีม** ในโครงสร้าง deployment เดิม โดยไม่เชื่อการซ่อน UI หรือค่าทีมที่ client ส่งมา

- Resolve actor จาก credentials ที่ตรวจสอบแล้วเท่านั้น; `team_id` และ role ที่ใช้ตัดสินสิทธิ์ต้องไม่มาจาก request body ที่ผู้ใช้แก้ได้
- ตรวจ actor, action และ resource ใน API ทุกเส้นทางที่เกี่ยวข้อง รวม read, search, update และ delete; เมื่อระบุสิทธิ์ไม่ได้ให้ปฏิเสธ
- จำกัด query ด้วย note ID และ team ID จาก trusted actor พร้อมใช้ bound parameters ทั้งหมด การ parameterize ป้องกันการเปลี่ยนโครงสร้าง SQL แต่ยังต้องมี authorization predicate อยู่ด้วย
- แก้ไขได้เฉพาะ field ที่อนุญาต เช่น `title` และ `content`; ไม่รับ `team_id` หรือ `owner_id` จากการแก้โน้ตปกติ
- กำหนด policy ของ owner/member/team admin เป็น permission matrix ก่อนลงมือ ไม่อนุมานว่า admin ใช้ได้ทุกทีม
- กำหนด public share เป็นข้อยกเว้นที่แยกชัด: token ให้สิทธิ์อ่าน resource ที่ระบุเท่านั้น ต้องตรวจความถูกต้อง อายุ และการเพิกถอนตาม contract; ไม่เปิดสิทธิ์ update/delete

ตัวอย่าง predicate สำหรับการอ่านโน้ตหลังตรวจ actor แล้ว (illustrative code; ยังไม่ได้แก้ implementation):

```sql
SELECT id, title, content, owner_id, team_id
FROM notes
WHERE id = @noteId AND team_id = @actorTeamId;
```

หากไม่พบ row ใน scope ให้ตอบ 404 สำหรับเส้นทาง read/delete ตาม contract ที่ tests เดิมคาดหวัง; ไม่ใช้ query นี้แทนการตรวจสิทธิ์ owner/admin ของ action อื่น และต้องเพิ่ม predicate/validation สำหรับการเขียนตาม permission matrix

## Consequences

**ประโยชน์:** กติกาใช้ร่วมกันทุก client, ขอบเขตข้อมูลตรวจได้จาก source และ tests, และทีมสามารถทยอยแก้โดยคง deployment เดิม

**ต้นทุน:** มีงานทบทวน endpoint และ tests ให้ครบ ต้องรักษา permission matrix ไม่ให้ drift การใช้ฐานข้อมูลร่วมกันยังมีความเสี่ยงถ้า query หรือ credential อื่นเข้าถึงข้อมูลนอก API ได้

**สิ่งที่ยังไม่แก้:** JWT validation, SQL injection, public-share token และ mass assignment ใน baseline ยังต้องมีงาน implementation และการตรวจผลแยกตามรายการด้านล่าง ADR นี้ไม่รับรองว่าไม่มีช่องโหว่อื่น และไม่ใช่ผล pentest

## Threat model links

| Threat ID (ตัวอย่างใน ADR นี้) | เส้นทาง / ความเสี่ยง | Control ที่เสนอ | หลักฐานที่ต้องใช้ตรวจ |
|---|---|---|---|
| T-TENANT-01 | ผู้ใช้เปลี่ยน note ID เพื่ออ่าน/ลบข้ามทีม | resource policy + team-scoped query | BOLA negative test + ตรวจว่า row ไม่ถูกลบ |
| T-TENANT-02 | client ส่ง `team_id` / `owner_id` เพื่อย้ายข้อมูล | allowlisted writable fields | mass-assignment tests + ตรวจ DB state |
| T-TENANT-03 | input ใน search เปลี่ยนความหมาย SQL และหลุด team scope | bound parameters + tenant predicate | SQLi regression และ happy path |
| T-IDENTITY-01 | actor/role/team ถูกปลอมผ่าน JWT | ตรวจ credentials ก่อน authorize | JWT negative tests; เป็น prerequisite ของ ADR นี้ |

Threat IDs เป็นชื่อที่กำหนดสำหรับเอกสารตัวอย่าง ไม่ใช่ SonarQube issue keys และไม่ใช่รายงานว่าทดสอบโจมตีสำเร็จแล้ว

## Verification and acceptance

ตรวจ source ของ snapshot ที่ระบุแล้ว แต่ **ไม่ได้รัน security tests หรือแก้ code ในการจัดทำ ADR นี้** รายการต่อไปนี้เป็นเกณฑ์รับ implementation ในอนาคต ไม่ใช่ผลทดสอบที่ผ่านแล้ว

- `api.Tests/Security/Lab4Tests.cs`: `User_A_cannot_read_user_Bs_note` คาด 404 และ `User_A_cannot_delete_user_Bs_note` ต้องตรวจว่า row ยังอยู่
- ไฟล์เดียวกัน: `Put_note_ignores_team_id_and_owner_id` ต้องคง team/owner เดิม; `A_team_admin_can_edit_a_teammates_note` และ `An_admin_can_use_the_admin_api` เป็น positive controls; member ต้องใช้ admin API ไม่ได้
- `api.Tests/Security/Lab2Tests.cs` และ `api.Tests/HappyPath/HappyPathTests.cs`: ใช้ตรวจ SQLi และการใช้งานปกติคู่กัน ไม่รับผลผ่านเพราะ endpoint หายหรือทุก request ถูกปฏิเสธ
- `api.Tests/Security/Day1AuthTests.cs`: ตรวจ authentication prerequisites ตาม test ที่มีจริง และเพิ่มกรณีที่ยังขาดก่อนอนุมัติ
- เพิ่มกรณีอ่าน/search/update/delete ของทุก role/ทีมที่ permission matrix ระบุ รวม direct API request และ public-share exception; tests ที่มีอยู่ยังไม่ถือว่าครอบคลุมทุกกรณี

## Revisit triggers and lifecycle

ทบทวนใหม่เมื่อเพิ่ม background worker หรือ client ที่อ่าน DB ตรง, เปลี่ยน tenant model, ต้องการ admin ข้ามทีม, เพิ่มข้อกำหนดแยกข้อมูลที่ชั้น DB หรือพบเส้นทางที่ policy เดิมครอบคลุมไม่ได้

เมื่อผู้รับผิดชอบยอมรับข้อเสนอจึงเปลี่ยนสถานะเป็น Accepted พร้อมวันและผู้อนุมัติ การเปลี่ยนสถานะไม่เท่ากับ implementation เสร็จ; ติดตามงานและผล verification แยกกัน หากภายหลังเลือกแนวทางใหม่ ให้สร้าง ADR ใหม่และ mark เอกสารนี้เป็น Superseded พร้อมลิงก์แทนการลบเหตุผลเดิม

## References

- [Michael Nygard — Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
- [ADR GitHub organization — definitions and resources](https://adr.github.io/)
- [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html)
- [OWASP SQL Injection Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/SQL_Injection_Prevention_Cheat_Sheet.html)
- [PostgreSQL — Row Security Policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)

ตัวอย่างนี้ใช้ข้อมูลจากไฟล์ในโปรเจกต์ ณ วันที่ระบุ ไม่ได้อ้างเวอร์ชัน library ว่าเป็นเวอร์ชันล่าสุด
