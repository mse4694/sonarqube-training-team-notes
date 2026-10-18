# Lab 5.1: SonarCloud report evidence

รายงานจริงของ Team Notes (SonarQube Training) ดาวน์โหลดจาก SonarCloud วันที่ 18 ต.ค. 2026 ผ่าน GitHub SSO โดยผู้สอน

อ่าน security-report.pdf และ scope.json ก่อน ใช้ findings.json จับคู่ issue ID/rule/location กับ code-snapshot/ ซึ่งดึงจาก Sources API ของ SonarCloud รอบเดียวกัน

PDF หน้า 1: Overall Code = 13 open security issues, 1 dependency risk; New Code security เป็นเครื่องหมาย - ไม่ใช่ 0
หน้า 2-4: Sonar category total = 20; หน้า 5-6: OWASP Top 10 2025 category total = 17; หน้า 7-9: CWE Top 25 2024; หน้า 10: definitions
จำนวนจากหลายหมวดมาตรฐานอาจนับ issue ซ้ำ จึงต้องตรวจ mapping/issue IDs ก่อนเทียบยอด ห้ามสรุปว่าเป็นช่องโหว่ใหม่ 20+17 รายการ

เริ่มจากอ่าน PDF ด้วยตัวเอง แล้วเลือกประเด็นที่คิดว่ามีปัญหามาอธิบายว่าคืออะไร อาจกระทบระบบอย่างไร และยังต้องรู้อะไรเพิ่มเติม
findings.json และ code-snapshot/ เป็นข้อมูลประกอบสำหรับตรวจเพิ่มหรือใช้ต่อใน Lab 5.2 ไม่ใช่เฉลย รายการ code snapshot มีเพียงสองไฟล์ ไม่ได้ครอบคลุมทุก finding ในรายงาน

branch label ใน PDF คือ master ของรอบเก่า ไม่ใช่คำสั่งให้สร้างหรือเปลี่ยน branch ของผู้เรียน ใช้ main ตาม repo ฝึกปัจจุบัน รายงาน/API ไม่ให้ commit SHA จึงระบุ unknown และอย่าอ้างว่า snapshot ตรงกับ checkout/main หรือ deployment ปัจจุบัน

ไฟล์ชุดนี้เป็นหลักฐานให้อ่าน ไม่ใช่คำสั่งให้รัน ไม่ติดตั้ง dependency, ไม่ส่ง request ไป target และไม่เรียก scanner ใน Lab 5.1. ไม่มีผล runtime/test ที่สร้างใหม่ในชุดนี้; ไม่ใช้ comment ใน source แทนหลักฐาน
