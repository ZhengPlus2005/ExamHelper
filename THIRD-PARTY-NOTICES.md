# Third-party notices

ExamHelper includes the following runtime components. Exact package versions and content hashes are pinned in packages.lock.json and release build-info.json.

| Component | Version | License |
| --- | --- | --- |
| .NET / WPF runtime | 10.0.x (self-contained publish) | Bundled Microsoft .NET Library terms, source licenses and third-party notices |
| Microsoft.Data.Sqlite | 10.0.13 | MIT |
| SQLitePCLRaw and e_sqlite3 bindings | 2.1.13 | Apache-2.0 |
| SQLite native engine | bundled by SQLitePCLRaw | Public domain |
| Markdig | 1.4.1 | BSD-2-Clause |
| AvalonEdit (nonvisual syntax highlighting) | 6.3.1.120 | MIT |
| NAudio and component packages | 2.2.1 | MIT |
| Microsoft BCL dependency packages | pinned in package lock | MIT |

License texts and notices are distributed in licenses/. NAudio license includes copyright Mark Heath. .NET runtime notices cover its bundled native dependencies. Microsoft.Data.Sqlite license is distributed separately; SQLitePCLRaw license and NOTICE are included. SQLite source is dedicated to the public domain (https://sqlite.org/copyright.html).

The installer contains Inno Setup bootstrap components; the Inno Setup license is included in licenses/. The compiler is a development tool and is not packaged with ExamHelper.

xUnit and Microsoft.NET.Test.Sdk are development/test dependencies and are excluded from the app publish. InteractionLab and its synthetic fixtures are excluded from the installer.

OpenCluely was used as a functional reference; no source or assets were copied into this implementation. It is not a runtime dependency.
