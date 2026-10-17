# 安装与发布验证

2026-10-17：已生成自包含 win-x64 发布目录及约 52.1MB 的 ExamHelper-Setup-0.1.0-x64.exe；相邻 .sha256 文件记录当前构建散列。安装器为未签名预览包，不把编译器签名当作产品签名。应用普通权限 asInvoker，安装器 PrivilegesRequired=lowest；默认当前用户安装至 %LOCALAPPDATA%\Programs\ExamHelper，创建当前用户开始菜单快捷方式。安装向导为英文，应用正文为中文。

本机 Windows 11 x64 build 26200，以非管理员令牌在 D:\Code\ExamHelper\artifacts\installation-check\app 完成安装、同版覆盖升级和卸载；三次退出码均为 0。已确认无已有 ExamHelper 安装记录后才执行，不覆盖用户已安装版本。用户数据目录中创建的唯一合成标记在升级及卸载后均保留，随后只清理该标记，未读取或删除真实设置／历史。卸载后应用文件移除。证据 installation-check/installation-result.json、install.log、upgrade.log、uninstall.log。

发布目录和安装后 EXE 的 --self-test 均通过：运行时 10.0.13、SQLite 3.53.3、内置测试图可加载，无网络请求、未读取用户设置。该自检只使用内存数据库并检查部署资源，不授予服务验证资格，不展示辅助窗口。宿主本身装有 SDK，因此不能冒充未安装 .NET 的干净机器测试，该环境验证仍待补充。安装器静态条件及应用原生版本检查拒绝 Windows 10、ARM、Server 和非 x64 进程；本机原生版本读取已实测。

Inno Setup 6.7.3 从[官方下载页](https://jrsoftware.org/isdl.php)列出的 GitHub 发布获取，运行前 Authenticode 验证为 Valid，发布者 Pyrsys B.V.。开发编译器安装在项目 .tools/InnoSetup6，使用当前用户模式，不随安装器分发。缺失编译器测试返回非零并说明下载／路径配置，不生成伪安装包。

scripts/Test.ps1 实际执行锁定恢复、Release 编译及测试发现数检查：127 项通过（Core59 Windows68）、零跳过，编译零警告零错误。scripts/Publish.ps1 保存 SDK、版本、Git SHA、工作树状态和锁文件散列；最终修复提交后重新构建的清洁发布信息以 build-info.json 为准。

产品数据位于 %LOCALAPPDATA%\ExamHelper。卸载默认保留该目录；需要移除历史或密钥时，应先备份，再由用户明确清理该目录。安装器不包含开发实验程序、测试程序集、音频、历史数据库或服务凭据。第三方许可随包附在 licenses/。
