# How to Release a New Riptide Update
**Make your last source code change *before* proceeding with the following steps!**

All paths are relative to the root repository directory.

## Update the Assembly Version
In `RiptideNetworking.csproj`:
1. Bump the version number in the `VersionPrefix` tag, using proper semantic version practices.
2. Update the version number of the release notes link in the `PackageReleaseNotes` tag.
3. Commit with message: `Bump version number to <version here>` (for example: `Bump version number to 2.1.0`).

## Add Release Notes
1. In the `Docs/manual/overview/installation.md` file, update the version number in step 4 of the Unity Package Manager installation instructions. Note that there are **3** places where the version number is mentioned—once in the package URL and twice in the text that follows. All 3 should be updated.
2. In the `Docs/manual/updates/release-notes` folder, create a new release notes file called `v<version here>.md` (for example: `v2.1.0.md`). It's easiest to duplicate an existing release notes file and rename & edit it. `v2.1.0.md` is a good template as it was a sizeable update with a number of additions, changes *and* fixes. The "sponsor shoutout" at the bottom does not need to be included.
    - Make sure to update the version number appropriately in the `_description` metadata, the `# v<version here> Release Notes` header, the Unity Package Manager install URL, and in the *version comparison link* (this has 2 version numbers, the first should be the previous version number, the second the version number that you're in the process of releasing).
    - Describe your additions, changes, and fixes in the appropriate sections, and link to any relevant issues or pull requests. These descriptions should use proper grammar & punctuation and be human readable!
3. Add the newly created file to the manual's table of contents by modifying the `Docs/manual/toc.yml` file.
4. Make sure the added/modified files work & look right by building the documentation site locally ([instructions](Docs/manual/guides/build-docs.md)).
5. Commit with message: `Add v<version here> release notes` (for example: `Add v2.1.0 release notes`)

## Update the Unity Package Version
1. Switch to the `unity-package` branch. This is a completely separate branch that shares no files with `main`, which is why I personally have the repository cloned twice (once for each branch).
2. Make sure to pull any changes—this branch is automatically committed to any time you push source code changes on `main`.
3. Open the `Packages/Core/package.json` file and update the version number in the `version` tag and in the link in the `changelogUrl` tag.
4. Commit with message: `Bump package version to <version here>` (for example: `Bump package version to 2.1.0`).
5. Tag this commit with a tag called `<version here>` (for example: `2.1.0`). **IMPORTANT:** do *not* include a `v` in this tag!
6. Push the commit *and* the new tag.

## Update the Demo Projects
1. Open all 3 Unity demo projects found in the `Demos/Unity` folder (there are 2 projects in the `DedicatedServerDemo` folder).
2. In each project, open the Unity package manager window, click the `+` icon in the top left, and choose the *Add package from git URL* option. This will replace the previous version.
3. Commit with message: `Update demos to v<version here>` (for example: `Update demos to v2.1.0`).
4. Tag this commit with a tag called `v<version here>` (for example: `v2.1.0`). **IMPORTANT:** *do* include a `v` in this tag!
5. Push the commit *and* the new tag.

## Build & Deploy the Docs Site
1. Go to the [Update Documentation Site](https://github.com/RiptideNetworking/Riptide/actions/workflows/update-pages.yml) workflow on the repository page.
2. Click the *Run workflow* dropdown, ensure the `main` branch is selected, and click *Run workflow*.

## Update the NuGet Package
**IMPORTANT:**
Ensure everything is *truly* ready before proceeding with this step! If you make a mistake or forget to commit a change/addition in one of the previous steps, fixing them merely makes the commit history a bit messier. **Once you publish a package version to NuGet you *cannot* modify it anymore**—in the worst case (like if you accidentally include sensitive info) you can delete/take down the published version, but you will *not* be able to publish the package with that same version number again.

I learned this the hard way when I forgot I still had unstaged/uncommitted changes—some log messages I was using to debug—and published v2.1.1 with those logs in it. There was nothing I could do to change it.

Once you're sure you're ready to proceed:
1. Open the `RiptideNetworking` solution in Visual Studio.
2. Ensure the build configuration is set to *Release* (there's a dropdown near the top left).
3. Right click the `RiptideNetworking` project in the Solution Explorer, and then click *Pack*. This will generate a .nupkg file.
4. In a terminal window, navigate to the `RiptideNetworking/RiptideNetworking/bin/Release/netstandard2.0` folder which contains the .nupkg file.
5. Run `dotnet nuget push RiptideNetworking.Riptide.<version here>.nupkg --api-key <API key here> --source https://api.nuget.org/v3/index.json` (for example: `dotnet nuget push RiptideNetworking.Riptide.2.1.0.nupkg --api-key qz2jga8pl3dvn2akksyquwcs9ygggg4exypy3bhxy6w6x6 --source https://api.nuget.org/v3/index.json`). If you need the API key, ask me—the example one is fake.

## Create a New GitHub Release
1. Go to the [releases page](https://github.com/RiptideNetworking/Riptide/releases).
2. Click *Draft a new release*.
3. Click *Choose a tag* and choose the version tag you created earlier on the main branch (it should have a `v` in it, like `v2.1.0`).
4. Set the title to `v<version here>` (for example: `v2.1.0`)
5. Write a description for the release.
    - Mention a few of the main features/changes/fixes.
    - You can use previous releases as a template (if you go to the edit option you can copy the raw text including the markdown formatting), but make sure you update the version number anywhere it's mentioned, like in the release notes and version comparison links.
6. In Visual Studio, ensure the build configuration is set to *Release* and build the `RiptideNetworking` project.
7. Attach the `RiptideNetworking.dll` and `RiptideNetworking.xml` files from the `RiptideNetworking/RiptideNetworking/bin/Release/netstandard2.0` folder to the release.
8. Click *Publish release*.

## Announce the Update
1. In the Discord server's `riptide-updates` channel, write an announcement message.
    - Tag the `Riptide Updates` role.
    - Briefly describe the release.
    - Use the previous announcement messages as a template, but make sure you update the version number and all the links.
    - For some reason Discord doesn't embed links if the link markdown is immediately followed by any character other than a space (` `). I take advantage of this and put a zero-width space (U+200B) after the closing parenthesis (`)`) of all GitHub links (`[<link text here>](<url here>)<zero width space here>`) to prevent them from embedding because GitHub embeds are massive and unnecessary, but I want the Riptide documentation embeds.
2. Send the message.
3. Publish the message so any servers that are subscribed to the channel (not sure if there are any) see it too.
