Candidate A fails the supplied renderer safety evidence: its recorded output contains executable JavaScript URLs and an event-handler attribute. Candidate B passes all supplied predicates, but that supports only the recorded inputs and renderer scope—not a general safety conclusion.

I read the five supplied files as evidence. I did not run candidate code, tests, scanners, or network requests.

### Evidence ledger

| ID | Source | Provenance and scope |
|---|---|---|
| E1 | `CandidateA.cs` | Source uses `UseAdvancedExtensions().DisableHtml()` and `Markdown.ToHtml`. E5 reports source SHA-256 `c4d264fe9c2c8f30efe44c462b21323491f4698b3fe809a275d116c6fe9c0145`. |
| E2 | `CandidateB.cs` | `TeamNotes.Api.MarkdownRenderer` selects extensions explicitly, disables HTML, and sanitizes parsed link/autolink URLs. E5 reports SHA-256 `e573a56dc56cf30f7f869445e50ec181cf07ae9f9f52cc75f4cc7cc0cf3fe4af`. |
| E3 | `provided-evidence.json` | Fourteen recorded render results: seven inputs for each candidate. Reports assembly `1.3.0.0`, informational version `1.3.2+fc705234fa211d179ee1d5e7656b51ab99f70ca9`. Contains HTML and predicate flags. |
| E4 | `provided-evidence-provenance.json` | Attributes the probe to Markdig `1.3.2`, repository `mikelopster/sonarqube-training-team-notes`; describes B as an unchanged copy of `origin/solution api/MarkdownRenderer.cs`. Reports isolated rendering, exit code 0, no HTTP/auth/DB/startup/network. |
| E5 | `source-validation.json` | Reports offline compilation/execution against a cached Markdig `1.3.2` DLL, exit code 0, and identical JSON to the original probe. Supplies candidate and harness source hashes. These are recorded assertions, not validation performed in this audit. |

E4’s additional build/artifact identifiers are:

- Compiled DLL SHA-256: `6231c6a7216466cbe7fdeab7463a30ed29275cf90042ba64b49e1205d45e7eb5`
- Container image: `sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317`
- Render-result SHA-256: `6d663573f789e853e644842634762d6bae79955e238396ca5c9d17fed61418a9`
- Harness `Program.cs` SHA-256, from E5: `63505c804c50e69e81e673ff33c9a73f0245e2f26e1f5bbc137c8d0d5af6ba3b`

The reported B hash matches E4’s solution-renderer hash. No immutable repository commit or complete build manifest is supplied, and I did not independently verify these hashes.

### Provided observations

Counts below derive from E3’s seven rows per candidate:

| Recorded outcome | A | B |
|---|---:|---:|
| Existing predicate passes | 3/7 | 7/7 |
| Existing predicate failures | 4/7 | 0/7 |
| Rows containing active JavaScript `href`/`src` | 3 | 0 |
| Rows containing an event-handler attribute | 1 | 0 |
| Formatting positive control passes | 1/1 | 1/1 |

These are case counts, not independent vulnerability counts. A’s three URL rows contain four JavaScript `href` occurrences because the combined case contains two links.

The supplied HTML distinguishes the relevant contexts:

- Both candidates emit the raw `<img>` and `<script>` examples as `&lt;...&gt;` text. The visible `onerror` text is inert in that output.
- A emits real `href="javascript:..."` attributes in the combined, link-only, and autolink-only cases.
- A emits a real `onmouseover="alert(1)"` attribute and attacker-selected `style` on the heading.
- B emits the generic-attribute syntax as heading text, with encoded quotation marks. Its generated heading ID contains related characters, but no event-handler attribute.
- B replaces the supplied JavaScript destinations with `#`.

Thus, substring presence alone is insufficient: escaped `onerror` text is different from an actual HTML event attribute. The recorded HTML establishes dangerous attributes; it does not establish that a browser executed them.

### Source-based inferences

E1’s HTML-disabling configuration does not prevent the URL and generic-attribute output demonstrated in E3.

E2 explicitly avoids the broad extension bundle and rewrites disallowed link URLs. Its URL check removes characters at or below ASCII space for classification, permits `http`, `https`, and `mailto`, and treats certain destinations as relative. This explains the supplied successful cases, but does not prove coverage of every URL normalization, Markdown construct, or browser interpretation. Comments claiming safety are not independent evidence.

### Falsifiable safety claim

**Claim:** For the recorded Markdig build, Candidate B renders this exact input with both anchor destinations neutralized:

```text
[click me](javascript:alert(document.cookie)) and <javascript:alert(1)>
```

**Acceptance predicate:** The rendered HTML is exactly the following, including a final LF:

```html
<p><a href="#">click me</a> and <a href="#">#</a></p>
```

It must also contain no actual event-handler attribute or JavaScript `href`/`src`. E3’s B row supports this narrow claim; A’s row fails it.

**Formatting positive control:** Exact input:

```text
**bold** and [docs](https://learn.microsoft.com/)
```

Accept only the following output, including a final LF:

```html
<p><strong>bold</strong> and <a href="https://learn.microsoft.com/">docs</a></p>
```

Both recorded candidates satisfy this control, showing preservation of these two formatting features.

**Rejection:** Reject the stated claim if its exact output predicate fails, particularly if either JavaScript destination survives as an active attribute. Reject formatting acceptance if the positive control fails.

**Inconclusive:** Treat results as inconclusive for this build-specific claim if source/build identity cannot be established, output is missing, or only predicate flags are available without inspectable HTML. Passing these cases cannot establish universal safety.

### Limitations and human-review gap

The evidence covers seven renderer inputs, with overlapping attack cases. It does not cover browser execution, endpoint delivery, authorization, persistence, application startup, all extensions, or comprehensive URL normalization. The “shared note” case explicitly covers only a renderer predicate. Predicate implementation and the probe harness are not supplied.

**One gap for human review:** establish that the deployed shared-note path uses this exact Candidate B build and inserts its output without transformations that could change HTML interpretation. The supplied renderer records do not establish that application-level connection.