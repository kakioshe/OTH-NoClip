"""Report coverage only for the production controller input source."""
import json
import os
import sys

with open(sys.argv[1], encoding="utf-8") as report_file:
    report = json.load(report_file)

documents = [
    classes
    for module in report.values()
    for path, classes in module.items()
    if path.replace("\\", "/").endswith("/ControllerInput.cs")
]
if len(documents) != 1:
    raise SystemExit("Expected coverage for exactly one ControllerInput.cs file")

lines = []
branches = []
for classes in documents:
    for methods in classes.values():
        for coverage in methods.values():
            lines.extend(coverage["Lines"].values())
            branches.extend(branch["Hits"] for branch in coverage["Branches"])
if not lines or not branches:
    raise SystemExit("Coverage is missing production line or branch data")

def summary(label, hits):
    covered = sum(hit > 0 for hit in hits)
    return f"{label}: {covered}/{len(hits)} ({100 * covered / len(hits):.1f}%)"

text = "\n".join([
    "ControllerInput.cs coverage (not whole-plugin coverage)",
    summary("Lines", lines),
    summary("Branches", branches),
])
print(text)
if os.environ.get("GITHUB_STEP_SUMMARY"):
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary_file:
        summary_file.write("## Controller input coverage\n\n" + text.replace("\n", "  \n") + "\n")
