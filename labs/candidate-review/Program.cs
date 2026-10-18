using Markdig;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
var candidateA = new CandidateAudit.CandidateA();
var candidateB = new TeamNotes.Api.MarkdownRenderer();
var cases = new[]{
 new{ id="Raw_html_in_markdown_is_escaped",markdown="Hello <img src=x onerror=alert(1)> <script>alert(2)</script>",kind="raw"},
 new{ id="Javascript_links_are_neutralized",markdown="[click me](javascript:alert(document.cookie)) and <javascript:alert(1)>",kind="urls"},
 new{ id="Generic_attributes_cannot_inject_event_handlers",markdown="# Agenda {onmouseover=\"alert(1)\" style=\"position:fixed;inset:0\"}",kind="attributes"},
 new{ id="A_shared_note_does_not_serve_attacker_html (renderer predicate only)",markdown="# Agenda\n\n<img src=x onerror=alert(document.domain)>",kind="shared"},
 new{ id="Markdown_formatting_still_renders",markdown="**bold** and [docs](https://learn.microsoft.com/)",kind="format"},
 new{ id="javascript_link_only",markdown="[click me](javascript:alert(1))",kind="urls"},
 new{ id="javascript_autolink_only",markdown="<javascript:alert(1)>",kind="urls"}
};
var results=new List<object>();
foreach(var c in cases){
 foreach(var candidate in new[]{"A","B"}){
  var html=candidate=="A"?candidateA.ToHtml(c.markdown):candidateB.ToHtml(c.markdown);
  bool containsJs=html.Contains("javascript:",StringComparison.OrdinalIgnoreCase);
  bool activeJs=Regex.IsMatch(html,@"<(?:a|img)\b[^>]*\s(?:href|src)\s*=\s*[""']\s*javascript:",RegexOptions.IgnoreCase);
  bool onAttribute=Regex.IsMatch(html,@"<[^>]*\son[a-z]+\s*=",RegexOptions.IgnoreCase);
  bool passed=c.kind switch{ "raw"=>!html.Contains("<img")&&!html.Contains("<script"),"urls"=>!containsJs,"attributes"=>!onAttribute,"shared"=>!html.Contains("<img"),"format"=>html.Contains("<strong>bold</strong>")&&html.Contains("href=\"https://learn.microsoft.com/\""),_=>false};
  results.Add(new{caseId=c.id,candidate,input=c.markdown,html,existingPredicatePassed=passed,containsJavascriptText=containsJs,hasActiveJavascriptHrefOrSrc=activeJs,hasOnEventAttribute=onAttribute});
 }
}
Console.WriteLine(JsonSerializer.Serialize(new{assemblyVersion=typeof(Markdown).Assembly.GetName().Version?.ToString(),informationalVersion=typeof(Markdown).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,results},new JsonSerializerOptions{WriteIndented=true}));
