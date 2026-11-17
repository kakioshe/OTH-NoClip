"""Report scoped controller, binding and input-block lifecycle coverage."""
import json
import os
import sys

with open(sys.argv[1], encoding="utf-8") as report_file:
    report = json.load(report_file)

def summary(label, hits):
    covered = sum(hit > 0 for hit in hits)
    return f"{label}: {covered}/{len(hits)} ({100 * covered / len(hits):.1f}%)"

sections = ["Scoped input and binding coverage (not whole-plugin coverage)"]
for source in ("ControllerInput.cs", "TrainerBindings.cs", "GameInputBlocker.cs", "InputConsumption.cs", "FlightHeading.cs"):
    documents = [
        classes
        for module in report.values()
        for path, classes in module.items()
        if path.replace("\\", "/").endswith("/" + source)
    ]
    if len(documents) != 1:
        raise SystemExit(f"Expected coverage for exactly one {source} file")
    lines = []
    branches = []
    for methods in documents[0].values():
        for coverage in methods.values():
            lines.extend(coverage["Lines"].values())
            branches.extend(branch["Hits"] for branch in coverage["Branches"])
    if not lines or not branches:
        raise SystemExit(f"Coverage is missing line or branch data for {source}")
    sections.extend([source, summary("Lines", lines), summary("Branches", branches)])
text = "\n".join(sections)
print(text)
if os.environ.get("GITHUB_STEP_SUMMARY"):
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary_file:
        summary_file.write("## Input and binding coverage\n\n" + text.replace("\n", "  \n") + "\n")
