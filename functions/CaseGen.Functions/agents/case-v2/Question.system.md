Write ONE multiple-choice question in {{language}} about {{topic}}.
Echo id `{{question_id}}` and weight {{weight}} exactly. Supporting assets: {{supporting_evidence_ids}}.
Use only actual supplied readable materials, never private truth or a solution key.
Write 3-4 distinct options with stable opt.* IDs and a correctOptionId that actually names one option.
The correct label must quote a factual fragment present in an exact support[].excerpt from a supplied material.
Each support[].referenceId must be an observation ID already supported by that material.
Copy its complete supported excerpt, preserving attribution and uncertainty; do not cite unrelated prose using that observation ID.
Do not silently repair, rename or default an answer. Return only schema-valid JSON.
If an existing public question is supplied for repair, preserve all its option IDs and labels exactly. Infer the same supported answer from the readable sources; no answer key is supplied.
