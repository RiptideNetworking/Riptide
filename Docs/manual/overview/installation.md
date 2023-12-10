---
_description: How to install Riptide in your project.
---

# Installation

There are a number of ways to install Riptide, depending on what you're working on and which tools you're using.

## Unity
### Option 1: Unity Package Manager

> [!NOTE]
> Installing Riptide via Unity's Package Manager requires you to have git installed on your computer!

1. In your Unity project, open the Package Manager (Window > Package Manager).
2. Click the `+` (plus) button in the top left corner of the window.
3. Select the *Add package from git URL...* option.
4. Enter the following URL: `https://github.com/RiptideNetworking/Riptide.git?path=/Packages/Core#2.1.2`. To install a version other than v2.1.2, replace the `2.1.2` after the `#` with your chosen version number.
5. Click 'Add' and wait for Riptide to be installed.

If you have errors in your project after installation or intellisense isn't working for Riptide's classes, go to Edit > Preferences > External Tools, make sure the box next to *Git packages* is checked, and then click *Regenerate project files*.

> [!TIP]
> If you'd like to use the latest in-development version of Riptide instead of an official release, enter `https://github.com/RiptideNetworking/Riptide.git?path=/Packages/Core#unity-package` in the git URL field. Keep in mind that doing this will get you the latest state of the repository, **which may include bugs and incomplete features!**

### Option 2: DLL File

If you prefer not to use Unity's Package Manager or that option doesn't work for you, you can also install Riptide by manually adding the compiled dll file to your project.

1. Either
    - download the `RiptideNetworking.dll` file from [the latest release](https://github.com/RiptideNetworking/Riptide/releases/latest) (or choose [a previous version](https://github.com/RiptideNetworking/Riptide/releases)), or
    - clone/download the [repository](https://github.com/RiptideNetworking/Riptide) and build the solution yourself.
2. Drop the `RiptideNetworking.dll` file anywhere into your Unity project's Assets folder.
3. Optional: add the <code><a href="https://github.com/RiptideNetworking/Riptide/blob/unity-package/Packages/Core/Runtime/UnitySpecific/MessageExtensions.cs">MessageExtensions</a></code> class and add it to your project. It's included in the Unity package but isn't part of the dll file.

> [!TIP]
> It's highly recommended that you also download the `RiptideNetworking.xml` file and drop that into your project alongside the dll file. This will allow your IDE's intellisense to display Riptide's API documentation.

## .NET Projects

The following installation steps are for Visual Studio users and may differ if you use a different IDE.

### Option 1: [NuGet Package](https://www.nuget.org/packages/RiptideNetworking.Riptide)

1. Right click your solution in the Solution Explorer.
2. Select *Manage NuGet Packages for Solution...*
3. Click the *Browse* tab.
4. Search for the `RiptideNetworking.Riptide` package and select it.
5. Check the box next to the project(s) you want to add the package to.
6. Choose the version you want to install from the dropdown.
7. Click *Install* and then click *OK* when prompted.
8. Click *I Accept* to accept the license terms.

### Option 2: DLL File

1. Either
    - download the `RiptideNetworking.dll` file from [the latest release](https://github.com/RiptideNetworking/Riptide/releases/latest) (or choose [a previous version](https://github.com/RiptideNetworking/Riptide/releases)), or
    - clone/download [the repository](https://github.com/RiptideNetworking/Riptide) and build the solution yourself.
2. Right click your project in the Solution Explorer.
3. Select *Add* and then select the *Project Reference...* option.
4. Click *Browse* in the left sidebar of the window.
5. Click the *Browse* button in the bottom right corner of the window.
6. Navigate to the folder where you saved the `RiptideNetworking.dll` file and add it.
7. Click *OK*.

### Option 3: Direct Project Reference

1. Either
    - download the `RiptideNetworking.dll` file from [the latest release](https://github.com/RiptideNetworking/Riptide/releases/latest) (or choose [a previous version](https://github.com/RiptideNetworking/Riptide/releases)), or
    - clone/download [the repository](https://github.com/RiptideNetworking/Riptide) and build the solution yourself.
2. Right click your solution in the Solution Explorer.
3. Select *Add* and then select the *Existing Project...* option.
4. Navigate to the cloned/downloaded Riptide repository and open the `RiptideNetworking.csproj` file.
5. Right click your project in the Solution Explorer.
6. Select *Add* and then select the *Project Reference...* option.
7. In the *Projects* tab (should be selected by default in the left sidebar), check the box next to *RiptideNetworking*.
8. Click *OK*.