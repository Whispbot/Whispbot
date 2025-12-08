## How to contribute to Whisp

In this file:
- Making changes
- Naming conventions
- Reporting bugs

## Making Changes
Want to change some code in the Whispbot repo?
- Firstly, create a fork of the repository. If you are planning on making changes to a branch other than main, make sure to uncheck the 'only copy main branch' option.
- Then, make the changes to your fork and commit them.
- Finally, create a pull request between the Whispbot repo and your fork and wait for it to be reviewed by the team.

## Naming Conventions
It is important that we have a standard when working with others. This section makes sure that you can understand the standards that we employ.
| Standard | Convention
|----------|---------------------------------------------|
| In Code  | [.NET Naming Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names#naming-conventions)
| Commits  | {tag}: {verb} {description} - E.G. feat: Added /example command
| Branches | {creator}-{name} - E.G. yellow-add-erlc

tldr; Use meaningful names over making it look good

### Tags
Tags are a section of text which lets a reader know what kind of commit they are looking at.
Our tags are as followed:
- feat: Adding or adjusting a new feature
- fix: Fixing a feature
- refactor: Rewriten or restructured code
- perf: Increased performance of code
- build: Updates dependancies, versions, etc.

## Reporting Bugs
**Never report security vulnerabilities using Github issues - follow <a href=".github/SECURITY.md">SECURITY.md</a>.**

When reporting a bug:
1. Search for other issues which are similar to make sure that you are not creating a duplicate bug report.
2. Press the New Issue button and select `Bug Report`.
3. Fill in the template while being as descriptive as possible - the more descriptive you are, the less questions will need to be asked to reproduce the issue.
4. Submit the report and wait for the team to get back to you.
