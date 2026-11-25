# Apex Liquid Glass interface refresh

The final visual revision keeps the native desktop architecture and streaming
pipeline, while organizing the controls around the most frequent actions.

- Slim icon navigation with a quiet active-state tint and persistent device count.
- Two-column dashboard: connected devices beside QR quick-connect, recent sessions
  beside USB setup. Empty saved-device controls are no longer shown.
- Wireless QR action first, manual code/IP entry in an expandable panel, nearby
  discovery below, and older-device controls in a separate expandable section.
- Settings grouped into Video, Audio & input, Capture, Appearance and Advanced.
- A shorter Studio toolbar, with secondary phone controls in More.
- Consistent rounded surfaces, readable input fields, styled selection states,
  restrained indigo accents, dark-aware title bars and refined light/dark palettes.
- Page fade/slide transitions (190–230 ms) and restrained hover fades (110–150 ms).
  They respect Windows animation preferences and the persisted Reduce motion
  switch. The native video viewport is not animated or copied into WPF.

The redesign compiled successfully. Windows Application Control prevented final
visual/runtime QA on this machine; see TEST-RESULTS.md. No visual inspection of
the final layouts or animation timing is claimed.
