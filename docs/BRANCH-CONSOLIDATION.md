# Consolidated development branch

Continue development on `basil/megacrew`. It merges the six former topic
histories on top of upstream `3eddae5`; each former topic tip is an ancestor.
Their names and exact commits are retained in `integration-manifest.json`.
Old PRs remain linked as review history after being superseded by the single
integration PR. Retiring branch names does not remove their commits.

The merge reconciles the current source tree with the published cumulative
0.3.7 packet, applied once to pristine 0.3.0 in an isolated reconstruction.
The initial consolidation matched all 17 normal host source files byte for byte.
The subsequent console fix intentionally changes `Source/MegaCrew.cs`; the
manifest retains the canonical hashes and records this reviewed deviation.
The 12 onboard/docking and nine navigation sources retain their topic content.
The merged build helper compiles nested sources, supports `--output`, rejects
an empty source tree and reports its count.

The checked-in MegaCrew DLL was rebuilt from all 40 current source files
(the original 38 plus two reviewed Duna planner files);
its hash and the source hashes are in the manifest. No KSP/Unity reference
assemblies or QA addon DLLs are packaged in that plugin. This new integrated
DLL is a build candidate, distinct from the stable 36-source DLL used for the
recorded runtime refusal test. It has no full flight qualification.

Validation completed before publication: 59 Python tests with no skips,
14 root C# runners, the actual duty-query/bridge variant, 2,000 navigation
propagation cases with zero failures, and the IVA catalog recording harness.
The direct refusal harness has 113 assertions; its real KSP observation and
remaining limitations are documented separately.

Historical ZIPs and source patches remain for provenance. Never extract the
0.3.0 ZIP over this checkout or stack cumulative patches onto its expanded
source. The README's staging command rebuilds from clean inputs. The legacy
`package_candidate.py` is the host-core packet helper; use the integration
staging builder for the complete addon/navigation candidate.

Human UI checks are tracked in issue #11, **Human Help Requests**. Keep raw
logs, local game copies, personal saves and server data outside the repository.
The full freeze/thaw QA launcher now includes the public native-load/reload
pipeline and phase-specific guards; it compiles but has not run this procedure.
The direct refusal result does not complete it. Source work continues while
Basil and Hamish play: no KSP launches, termination or install changes until
Basil explicitly releases the runtime hold.

Current follow-up checks: 82 Python tests, 608 Duna model assertions and the
console input-lock boundary harness pass. A temporary full-snapshot build from
6df8af4 compiled all 40 sources with byte-identical Git source inputs and only
the normal plugin DLL. Prepare-only emitted no DLL. Its output hash and compiler
provenance are recorded in the manifest separately from the checked-in binary.
This source-only staging check does not install or launch KSP.
