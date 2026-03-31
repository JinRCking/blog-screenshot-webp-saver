# Blog Screenshot WebP Saver

`Blog Screenshot WebP Saver` 是一个面向博客作者的 Windows 小工具。

很多人在写博客时，会直接用 `Win + Shift + S` 截图，然后把图片插入文章。但 Windows 自带截图通常会以体积偏大的图片格式进入剪贴板，后续如果直接保存为 `.jpg` 或直接上传原图，常常会带来两个问题：

- 图片体积大，博客页面加载慢
- 服务器存储占用高，长期积累更浪费

这个工具启动后会在后台常驻监听剪贴板中的截图内容。当你使用 `Win + Shift + S` 截图后，它会自动把截图转换为 `.webp` 文件，并保存到默认目录：

`D:\1A-blog-webp-jietu\April`

对博客写作者来说，这样可以把“截图 -> 压缩 -> 转格式 -> 保存”变成一步完成。以常见的全屏截图为例，原始图片可能在 `3MB ~ 5MB` 左右，转成 `.webp` 后通常可以维持在 `100KB ~ 200KB` 左右，更利于网站访问速度和服务器空间控制。

## 功能特点

- 启动后托盘常驻，不影响正常使用
- 监听 `Win + Shift + S` 产生的截图剪贴板图片
- 自动转换为 `.webp`
- 默认保存到 `D:\1A-blog-webp-jietu\April`
- 附带桌面版 Python 压缩脚本，方便单独处理某张图片
- 双击即可运行，无需手动每次改路径

## 软件逻辑

主程序是一个基于 WinForms 的后台监听工具，核心流程如下：

1. 启动后注册剪贴板监听
2. 检测到新的截图图片后，从剪贴板读取图像
3. 先把图片临时保存为 PNG
4. 调用 Thonny 自带 Python 环境中的 Pillow
5. 运行 `convert_to_webp.py` 把临时图片转换为 `.webp`
6. 保存到 `D:\1A-blog-webp-jietu\April`

程序还做了简单的重复截图去重，避免同一张图在极短时间内被重复处理。

## Python 脚本说明

仓库里包含两个 Python 脚本：

- `convert_to_webp.py`
  说明：这是桌面程序内部调用的转换脚本。它接收“输入图片路径”和“输出图片路径”，直接生成 `.webp` 文件。

- `scripts/desktop_screenshot_webp.py`
  说明：这是根据你桌面上的 `压缩webp.py` 思路整理出来的脚本版本，保留了“超过目标大小就逐步降低质量”的压缩逻辑，默认输出目录是 `D:\1A-blog-webp-jietu\April`。

如果你电脑桌面上已经有：

`C:\Users\JinRC\Desktop\截图压缩webp.py`

那它就是这个逻辑的桌面可用版本。

## 压缩逻辑

桌面版 Python 脚本使用以下逻辑进行压缩：

- 目标大小：`50KB`
- 初始质量：`80`
- 最低质量：`30`
- 每次递减：`5`

处理流程：

1. 先按当前质量保存为 `.webp`
2. 检查文件大小是否小于等于目标大小
3. 如果仍然过大，就把质量继续降低
4. 一直到达到目标，或者降到最低质量为止

这套逻辑适合博客配图场景，能够在清晰度和体积之间做一个比较实用的平衡。

## 目录结构

```text
blog-screenshot-webp-saver/
├─ README.md
├─ .gitignore
├─ src/
│  └─ ScreenshotWebpSaver/
│     ├─ Program.cs
│     ├─ NativeMethods.cs
│     ├─ ScreenshotMonitorForm.cs
│     ├─ ScreenshotWebpSaver.csproj
│     ├─ convert_to_webp.py
│     └─ start_screenshot_webp_saver.cmd
├─ scripts/
│  └─ desktop_screenshot_webp.py
└─ release/
   └─ win10-x64/
      ├─ ScreenshotWebpSaver.exe
      ├─ ScreenshotWebpSaver.dll
      ├─ ScreenshotWebpSaver.deps.json
      ├─ ScreenshotWebpSaver.runtimeconfig.json
      ├─ convert_to_webp.py
      └─ start_screenshot_webp_saver.cmd
```

## 使用方法

### 方式一：直接运行 exe

进入：

`release/win10-x64/`

双击：

- `ScreenshotWebpSaver.exe`
  或
- `start_screenshot_webp_saver.cmd`

启动后软件会在后台托盘运行。

然后直接按：

`Win + Shift + S`

截图完成后，生成的 `.webp` 文件会自动保存到：

`D:\1A-blog-webp-jietu\April`

### 方式二：手动用 Python 脚本处理单张图片

```powershell
& "C:\Users\JinRC\AppData\Local\Programs\Thonny\python.exe" ".\scripts\desktop_screenshot_webp.py" "C:\path\to\your-image.jpg"
```

如果不传输出目录，脚本默认保存到：

`D:\1A-blog-webp-jietu\April`

## 运行环境

- Windows 10
- .NET Desktop Runtime 9
- Thonny 自带 Python
- Pillow

当前程序默认调用：

`C:\Users\JinRC\AppData\Local\Programs\Thonny\python.exe`

如果未来更换了 Python 安装路径，可以在 `ScreenshotMonitorForm.cs` 中修改对应常量。

## 适用场景

- 写博客时需要频繁截图
- 想直接使用 `.webp` 插图
- 希望减小网页图片体积
- 希望节约服务器空间与带宽

## 说明

这个项目偏向个人博客写作效率工具，默认路径已经按照当前使用场景配置好。如果你有自己的截图目录、月份目录或者博客资源目录，可以自行改源码中的保存路径常量。
