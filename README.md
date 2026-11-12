<p align="center"><img src="yak/icon.png" width="96" alt="Shrike"></p>

# Shrike

Shrike is a side-bar toolbox for **Rhino 8 (Windows)** that turns Python scripts
into one-click tools.

Write and test a script in Rhino's Script Editor (by hand, or with the help of
ChatGPT, Claude or any other assistant), then add it to Shrike. From then on it
is a button in the Shrike panel, with a name, an icon and a category.

## Features

- One-click Python tools in a dockable panel, grouped into collapsible categories
- Icons, colors and favorites for tools
- Version history: every replaced script is kept, and any version can be restored
- Share tools as `.shriketool` files and import tools from others
- Hot reload: changes to tool folders on disk show up in the panel right away
- Keeps each script's own `#! python2` / `#! python3` directive, so a tool runs
  exactly as it did in the Script Editor

## Install

In Rhino 8, run the `PackageManager` command, search for **Shrike** and click
Install. Restart Rhino. The panel docks next to Layers on first start; run the
`Shrike` command to open it again later.

## How tools are stored

Each tool is a folder with a `tool.json` and a script:

```
Documents\Shrike\Tools\
  My Tool\
    tool.json
    main.py
    icon.png          (optional)
    .shrike\history\  (saved versions, managed by Shrike)
```

```json
{
  "Id": "my-tool",
  "Name": "My Tool",
  "Category": "Modeling",
  "Type": "python3",
  "Entry": "main.py",
  "Icon": "icon.png",
  "Tooltip": "What the tool does"
}
```

Shrike reads tools from `Documents\Shrike\Tools` and from a `Tools` folder next
to `Shrike.rhp` (tools bundled with the plug-in).

## License

[MIT](LICENSE)
