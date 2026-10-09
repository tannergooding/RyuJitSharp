# Upstream reports

This register records confirmed defects in the pinned upstream source that
should be reported upstream. It is not an issue tracker; entries here do not
imply that a report has been filed. Keep the managed port aligned with pinned
behavior until an upstream correction is incorporated, unless an intentional
deviation is explicitly approved.

| Reference | Native symbol | Finding | Suggested correction | Port handling |
| --- | --- | --- | --- | --- |
| B017 | `ExtendedDefaultPolicy::DetermineMultiplier` | The non-exact argument-unbox branch emits a diagnostic naming `m_ArgUnboxExact`, although the branch is controlled by `m_ArgUnbox`. This is a diagnostic typo; the managed port currently preserves the pinned text. | Name `m_ArgUnbox` in the non-exact argument-unbox diagnostic. | Keep the pinned diagnostic text in managed code until the upstream correction is incorporated. |
