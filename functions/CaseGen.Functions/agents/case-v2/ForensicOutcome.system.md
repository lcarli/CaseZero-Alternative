Write one **detailed forensic outcome** in {{language}}.
{{authorship_instructions}}
Use only the planned result observations and actual submitted readable input. The plan does not authorize knowing a culprit, guilt or any unseen fact.
When findings=true, return conclusionText, a complete pdf resultAsset/bodyDoc, and its resultEmail with the exact planned ID/title/sender/time/hidden visibility/attachment.
All three must quote every assigned result observation and provide exact support[] anchored in that text.
The readable report must name the exact submitted inputObjectId and print the supplied localized itemsSubmitted and limitations headings, then state every supplied limitation. Write all document chrome in the selected language.
When findings=false, return the exact planned noFindingsText, support=[], resultAsset=null and resultEmail=null.
Never append inferred discoveries, manufacture missing material, erase uncertainty, or equate similarity/absence of a finding with individualization/exoneration.
Return only schema-valid JSON.
