# Contributing

Thank you for your interest in contributing to RiptideNetworking.

Please follow these guidelines to help the contribution process go more smoothly and efficiently.

## Ways to Contribute

### Bugs & Issues

If you find a bug in the source code or want to request a feature/improvement, you can [create an issue](https://github.com/tom-weiland/RiptideNetworking/issues/new), but please check the [existing issues](https://github.com/tom-weiland/RiptideNetworking/issues) before doing so to avoid creating duplicates.

#### How to Submit a (Good) Bug Report

When creating a bug report, please:
* choose a clear and descriptive title
* describe the problematic/unexpected behavior and when it occurs
* describe the behavior you expected to see instead, and why
* include any error messages and relevant code (use [Markdown code blocks](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#quoting-code))
* include the version of Riptide you're using
* include the name and version of the operating system you're using
* provide a simplified bare-bones project with exact steps on how to use it to reproduce the problematic/unexpected behavior

Additionally, it may be helpful to answer the following:
* Did the issue start happening recently (such as after an update) or was it always a problem?
* Can you consistently reproduce the issue? If not, under what circumstances does it (generally) occur? How frequently does it occur?

#### How to Submit a (Good) Feature Request

When creating a feature request, please:
* choose a clear and descriptive title
* describe the change/addition you'd like to see
* describe how and why the change/addition would be helpful
* include the name and version of the operating system you're using (if relevant)
* provide a specific use case for the change/addition. What you're trying to do may already be possible with existing functionality

### New Content & Features

If you'd like to add new features, improve existing functionality, or fix [an issue](https://github.com/tom-weiland/RiptideNetworking/issues), you can [create a pull request](https://github.com/tom-weiland/RiptideNetworking/compare). However, please first check the [existing pull requests](https://github.com/tom-weiland/RiptideNetworking/pulls) to avoid creating duplicates.

#### How to Submit a (Good) Pull Request

When creating a pull request, please:
* choose a clear and descriptive title
* format your commit messages properly (see below)
* use the same coding style as the rest of the project
* keep it small—reviewing 5 small pull requests is easier than reviewing 1 large one. Each pull request should contain only a single bug fix or feature addition
* explain in detail what you changed/added and why
* include a link to any related issues (if applicable)
* be prepared to accept suggestions to improve your pull request

#### Commit Message Formatting

A proper commit message's first line should:
* have its first word begin with an uppercase letter
* be kept to 50 characters or less (if at all possible)
* be written in the [imperative mood](https://en.wikipedia.org/wiki/Imperative_mood)
* NOT end with a period or other punctuation mark

The imperative mood is a grammatical mood that forms a command or request. To use it, use the base form of the verb. For example:
```sh
$ git commit -m "Add contributing guidelines"
```

If you're unsure whether your commit message is written in the imperative mood, ask yourself whether or not it makes sense when inserted into the following sentence: "If applied, this commit will `your commit message goes here`."
- [x] If applied, this commit will `Add contributing guidelines`.
- [ ] If applied, this commit will `Added contributing guidelines`.
- [x] If applied, this commit will `Make change x`.
- [ ] If applied, this commit will `Made change x`.

Feel free to describe your changes in more detail below the commit message, but include an empty line to separate the message body from the title. It should look like this:
```
Brief description of commit here

The more detailed description goes here, after one line of whitespace.
This can be as long as you want, and lines do not need to be limited
to 50 characters. However, please use proper grammer and punctuation.
```

#### Coding Conventions

Please write descriptive, readable code, keep things as simple as possible, and follow the naming conventions laid out in the [editorconfig](.editorconfig) file (IDEs such as Visual Studio will make you aware if you don't). Networking is complicated—comment anything that isn't obvious! Additionally:
* Use 4 spaces instead of tabs.
* Use string interpolation such as `$"Current value: {someVariable}"` instead of `"Current value: " + someVariable`.
* Avoid abbreviations. Use `rigidBody` instead of `rb`. Descriptive and readable names make for nicer code!
* Avoid using curly braces `{}` for single line if statements, loops, etc.
* Avoid prefixes such as `m_`.

Thanks :)