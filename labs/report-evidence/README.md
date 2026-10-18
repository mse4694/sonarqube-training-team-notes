# Offline SonarCloud security evidence

เอกสารสำหรับวิเคราะห์ Security Report ของ **Team Notes (SonarQube Training)** อ่านจากไฟล์ใน repository นี้ได้ทั้งหมด ไม่ต้องมีบัญชีหรือสิทธิ์เข้า SonarCloud

## เริ่มอ่าน

1. เปิด [รายงานรายละเอียด PDF — 39 หน้า](security-evidence-detailed.pdf) หรือ [HTML ฉบับเดียวกัน](security-evidence-detailed.html) เริ่มที่สารบัญ issue แล้วเลือก 1–2 ประเด็น
2. อ่าน issue ID, rule, severity/status, file/line, code excerpt, flow ที่เครื่องมือรายงาน และคำอธิบายของ rule ก่อนเขียนข้อสรุปของตนเอง
3. ใช้ [PDF ต้นฉบับแบบสรุป](security-report.pdf) เทียบภาพรวม Sonar, OWASP และ CWE; ตัวเลขในหลายหมวดอาจนับ issue ซ้ำ จึงไม่บวกเป็นจำนวนช่องโหว่ใหม่

รายงานรายละเอียดเป็น **เอกสารประกอบที่จัดรูปแบบจากข้อมูลจริงที่ส่งออกจาก SonarCloud** ไม่ใช่ PDF native ที่ SonarCloud สร้าง และไม่ใช่ผล scan หรือทดสอบระบบรอบใหม่

## ข้อมูลที่ให้

| ไฟล์ | ใช้ตรวจอะไร |
| --- | --- |
| [detailed/bundle.json](detailed/bundle.json) | ข้อมูลชุดเดียวกับที่ใช้จัดรายงาน: 13 security issues, 10 rules, 10 source files และ dependency risk 1 รายการ |
| [detailed/issues.json](detailed/issues.json) | Issue ID, message, location/range, status, severity/impacts และ flow locations |
| [detailed/rules.json](detailed/rules.json) | คำอธิบายสาเหตุ ความเสี่ยง แนวทางแก้ และ mapping ของ rule |
| [detailed/sources/](detailed/sources/) | Source ที่ส่งออกพร้อมเลขบรรทัดในรายงาน; ค่า credential ถูกปิดบัง |
| [detailed/source-manifest.json](detailed/source-manifest.json) | Hash ของต้นทางและฉบับเผยแพร่ ตำแหน่งที่ปิดบัง และข้อจำกัดเรื่อง revision |
| [detailed/dependency-risks.json](detailed/dependency-risks.json) | รายละเอียด Newtonsoft.Json 12.0.3 / CVE-2024-21907, package path และข้อมูลที่ SCA คืนมา |
| [detailed/security-metrics.json](detailed/security-metrics.json) | 41 metrics ที่บริการคืนมา พร้อมชื่อที่ไม่ถูกคืนและขอบเขตข้อมูล |
| [detailed/security-groupings.json](detailed/security-groupings.json) | Facets/กลุ่มข้อมูล security จาก API |
| [detailed/project-metadata.json](detailed/project-metadata.json) | Branch/analysis และ Quality Gate ที่ส่งออก |
| [detailed/collection.json](detailed/collection.json) | วันส่งออก, query, pagination และการตรวจความครบของชุดข้อมูล |
| [detailed/hotspots.json](detailed/hotspots.json) | ผล query Security Hotspots: 0 รายการในชุดที่ส่งออก |
| [detailed/omitted-fields.json](detailed/omitted-fields.json) | รายชื่อ field ของบัญชี/ผู้ใช้ที่ตัดออกก่อนเผยแพร่ |

`scope.json`, `findings.json` และ `code-snapshot/` เดิมเก็บไว้คู่กับ PDF สรุปสำหรับตรวจย้อนกลับ ใช้ชุด `detailed/` เมื่อต้องการข้อมูลประกอบที่ครอบคลุมกว่า

## อ่านวันที่และขอบเขตให้ถูก

- Core analysis: **10 October 2026 07:40:51 UTC**; analysis ID `651e6e22-465a-40ac-8548-6e8c7bad1e10`; branch label ที่บริการรายงานคือ `master`
- วัน export เป็น **18 October 2026** และแยกไว้จากวัน analysis; ไม่ได้สั่งให้ scan ใหม่
- SCA dependency analysis อัปเดต **16 October 2026** เป็นคนละชุดเวลากับ core analysis; รายงานเก็บ `MISSING_LOCKFILE` และข้อจำกัดของ package inventory ไว้ ไม่กล่าวว่าได้ inventory ครบ 221 packages
- ไม่มี analyzed commit SHA ที่บริการคืนมา จึงคง **unknown** ไม่ถือว่า snapshot ตรงกับ `main` หรือ deployment ปัจจุบัน และไม่ต้องเปลี่ยน branch ของงานฝึก
- Quality Gate API คืน `NONE` พร้อม conditions ว่าง จึงไม่ตีความว่า `PASS`; ข้อมูลที่ไม่คืนไม่ใช่ศูนย์
- `hasReachableLocations` เป็น flag ของการวิเคราะห์จากเครื่องมือ ไม่ใช่หลักฐาน exploit หรือ runtime reachability ที่เราทดสอบเอง

## ใช้ทำ Lab

ในข้อวิเคราะห์ให้ระบุ issue/risk ID, rule หรือ CVE, file/line หรือ package/version และหน้าหรือหัวข้อที่อ้าง แยกข้อความของเครื่องมือออกจากข้อสันนิษฐานของผู้เรียน แล้วระบุข้อมูลที่ยังต้องตรวจ

ชุดนี้มีไว้ให้อ่านและวิเคราะห์ ไม่มีคำสั่งให้ต่อ SonarCloud, ติดตั้ง dependency, ส่ง request ไป target, patch, ทดสอบ หรือเรียก scanner. Proposed fix ยังไม่ใช่ผลแก้ที่ยืนยันแล้ว
