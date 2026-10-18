# Lab 10.1: Offline SonarCloud evidence

ชุดข้อมูลนี้ส่งออกจาก SonarCloud ของผู้สอนสำหรับวิเคราะห์แบบ offline ไม่ต้อง login, ใช้ MCP, สร้าง project, ติดตั้ง integration หรือ scan ใหม่

## อ่านอะไร

- `sonarcloud-issue-evidence.pdf`: ภาพ issue/rule จริง, metadata และ source ที่ส่งออก
- `evidence.json`: issue `AaEkwyrvn3atgy46CG_O`, rule `csharpsquid:S3330` และขอบเขตหลักฐาน
- `AuthEndpoints.sonar.cs.txt`: source จาก SonarCloud ตามที่ส่งออกจริง
- `AuthEndpoints.learner-baseline.cs.txt`: source ใน learner repository ณ commit `158536e0ec903fc7fa297adcf81116b923280878`
- `source-comparison.diff` และ `source-identity.json`: ความต่างและ hashes ที่ตรวจซ้ำได้
- `rule-context.json`: ชื่อ rule, classification และมาตรฐานที่เกี่ยวข้อง ไม่ใช่ผลตรวจ runtime
- `screenshots` ในโฟลเดอร์นี้: ภาพส่วน issue/rule จาก browser จริง ตัด navigation ของบัญชีออก

## Identity และข้อจำกัด

Project: `mikelopster_sonarqube-training-team-notes` / branch label: `master`
Analysis ID: `651e6e22-465a-40ac-8548-6e8c7bad1e10`
Analysis date: `2026-10-10T07:40:51+0000`
SCM SHA ของ analysis: **unknown** เพราะ API ที่ตรวจไม่แสดงค่านี้

ไฟล์ทั้งสอง **ไม่ตรงกันทั้งไฟล์**: learner baseline มี argument/logger และบรรทัด logging เพิ่มจาก source ใน report
Cookie block ของ finding ตรงกันทุกอักขระ: SonarCloud lines 35–39 ตรงกับ learner lines 41–45. อย่าใช้เลขบรรทัดจาก report ไปแก้ไฟล์อื่นโดยไม่ตรวจ
Matching block hash: `b8e8d895181948def21f497b42f525dc392a12c7a407ea0c966380c06ce332b7`
การเทียบ block ยืนยันได้เฉพาะตำแหน่ง finding นี้ ไม่ได้ยืนยันทั้ง build, deployment หรือ runtime

## ตัวอย่าง prompt สำหรับวิเคราะห์ offline

```text
อ่าน README.md, evidence.json, source-identity.json, rule-context.json,
AuthEndpoints.sonar.cs.txt, AuthEndpoints.learner-baseline.cs.txt และ source-comparison.diff ที่แนบ
วิเคราะห์ S3330 หนึ่งรายการจากหลักฐานที่ผู้สอนส่งออก
แยก tool claim, source fact และสิ่งที่ยังไม่ทราบ
อธิบายเส้นทางจากการสร้าง session token ถึง cookie และผลของ HttpOnly
เทียบ finding กับ cookie block ใน source ปัจจุบันก่อนเสนอการแก้เฉพาะจุด
เสนอ regression test จาก api.Tests/Security/Day1AuthTests.cs (Day1CookieFlagTests) โดยยังไม่แก้หรือรัน
อย่าอ้างว่า source ทั้งไฟล์หรือ build ตรงกัน, อย่าต่อ SonarCloud/MCP และอย่าสร้างผลทดสอบขึ้นเอง
```

ผู้เรียนตรวจข้อเสนอจาก code จริงก่อนอนุมัติ scope. ชุดหลักฐานนี้ไม่มีเฉลย patch หรือผล tests หลังแก้
`api.Tests/Security/Day1AuthTests.cs` ใน repository มี `Day1CookieFlagTests` ให้ตรวจอ่านต่อโดยไม่ต้องเปิด SonarCloud

## แหล่งที่มา

- Issue: https://sonarcloud.io/project/issues?id=mikelopster_sonarqube-training-team-notes&open=AaEkwyrvn3atgy46CG_O&issues=AaEkwyrvn3atgy46CG_O
- Report overview: https://sonarcloud.io/project/security_reports?id=mikelopster_sonarqube-training-team-notes
- Exported at: `2026-10-18T06:14:47.740656+00:00`

ภาพและข้อมูลเป็น snapshot ณ เวลาที่ส่งออก ไม่ใช่สถานะ realtime
