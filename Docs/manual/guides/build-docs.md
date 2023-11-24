---
title: Build Docs Locally
_description: How to build Riptide's documentation locally on your computer.
---

# How to Build Documentation Locally

Building the documentation site locally on your computer can be useful in a variety of situations, such as when you wish to preview changes you've made.

Riptide uses [DocFX](https://dotnet.github.io/docfx) to build its documentation. To install DocFX, open a terminal and run the following command:
```bash
dotnet tool install -g docfx --version 2.70.1
```

Once DocFX is installed:
1. Navigate to the `Docs` folder in your cloned Riptide repository.
2. Run `docfx docfx.json --serve`.
3. Visit [http://localhost:8080/](http://localhost:8080/) in your web browser.