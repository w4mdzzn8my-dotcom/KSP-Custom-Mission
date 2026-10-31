# Onboard navigation preview candidate

The toolbar panel is a read-only candidate for checking a lower-periapsis
trajectory from inside the ship. It is an overlay, not a physical IVA prop.
Compilation has been checked; every in-game check below remains pending.

## Use and scope

In flight, toggle the circular crosshair icon in the stock application launcher.
The panel displays active vessel/body and current orbit. Enter a positive target
periapsis in kilometres and a positive lead time in seconds using a decimal point,
then refresh. Supported inputs are near-circular, unpacked orbiting vessels with
no existing nodes. The panel shows proposed burn UT, signed prograde delta-v and
detached predicted periapsis. It discards stale results and explains rejections.

This preview does not create a node, execute a burn, advance time, establish two
human approvals or model finite thrust, terrain, atmosphere or SOI encounters.
The detached prediction must be compared with stock KSP before any later node
creation feature is accepted. No engine/resource effect is implied by a result.

## Disposable runtime checklist — pending

Use the isolated navigation QA save after the overnight runtime restriction.
Preserve original saves and server data. Build to a temporary output with
`tools/build.py --output` and integrate with the current campaign source before
installing a test candidate; this branch's expanded base is older.

| Check | Required observation | Status |
| --- | --- | --- |
| Toolbar and IVA | Open/close from external view, IVA and map; readable and draggable | Pending |
| Keyboard and mouse | Editing numbers never stages, throttles or steers; window clicks do not operate background controls | Pending |
| Lock cleanup | Hide, defocus, pause/resume, vessel switch and scene exit restore controls | Pending |
| Invalid input | Empty, negative, comma decimal, NaN, infinity and overflow reject clearly | Pending |
| Valid orbit | Known circular fixture predicts target within declared tolerance | Pending |
| Stock comparison | Disposable CircularNodeProbe agrees with detached result and removes its exact node | Pending |
| Stale result | Vessel/body/patch/node/input change, real orbit perturbation or expired burn UT hides prior result | Pending |
| Unchanged orbit | Normal time progression/element reparameterization preserves a physically unchanged prediction until expiry | Pending |
| Unsupported states | Packed, landed, excessive eccentricity and invalid target reject without changing flight | Pending |
| Combined campaign | Campaign/room panels and preview coexist; no GUI or event errors on reload | Pending |
| Read-only behavior | Node count, engine/staging/warp and resource state show no action caused by refresh | Pending |

Record the source/assembly/dependency hashes and observed results. A compiler pass
or pure-model test does not complete any row. Until these checks pass, the panel
remains a draft feature rather than a qualified flight station.
