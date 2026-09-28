using SnnuAutoLogin.Core;
using SnnuAutoLogin.Logging;
using SnnuAutoLogin.Security;

namespace SnnuAutoLogin.Ui;

/// <summary>
/// 配置窗体（首次运行向导 + 日常修改）。
/// 安全要点：密码框留空表示沿用已存密码；保存时经 DPAPI 加密后落盘，窗体任何时刻不持久化明文。
/// </summary>
internal sealed class ConfigForm : Form
{
    private readonly ConfigStore _store;
    private readonly AppCoordinator _coordinator;

    private readonly TextBox _studentId = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly ComboBox _service = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _autoStart = new() { Text = "开机自动启动并检测", Checked = true };
    private readonly Label _hint = new();
    private readonly ErrorProvider _errors = new();

    public ConfigForm(ConfigStore store, AppCoordinator coordinator)
    {
        _store = store;
        _coordinator = coordinator;

        Text = "陕师大校园网自动认证 · 配置";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        // 显式指定字体：不同 Windows/区域设置的默认字体度量不同，固定下来保证布局可预期
        Font = new Font("Microsoft YaHei UI", 9F);
        ClientSize = new Size(460, 300);

        var lblTitle = new Label
        {
            Text = "填写校园网认证信息（与门户登录页一致）",
            AutoSize = true,
            Location = new Point(16, 14),
        };
        var lblId = new Label { Text = "学号：", AutoSize = true, Location = new Point(16, 50) };
        _studentId.Location = new Point(104, 47);
        _studentId.Width = 336;

        var lblPwd = new Label { Text = "密码：", AutoSize = true, Location = new Point(16, 84) };
        _password.Location = new Point(104, 81);
        _password.Width = 336;

        var lblSvc = new Label { Text = "服务类型：", AutoSize = true, Location = new Point(16, 118) };
        _service.Location = new Point(104, 115);
        _service.Width = 336;
        foreach (var opt in ServiceTypes.All)
        {
            _service.Items.Add(opt);
        }

        _autoStart.Location = new Point(104, 148);

        // 固定矩形 + 关闭 AutoSize：不同 DPI/字体下文字不会因换行计算异常被裁剪
        _hint.AutoSize = false;
        _hint.ForeColor = Color.DimGray;
        _hint.Location = new Point(16, 176);
        _hint.Size = new Size(428, 60);
        _hint.Text = "密码仅以 Windows 加密（DPAPI）保存在本机，不会明文存储或上传。\r\n已配置过密码时，密码框留空表示不修改。";

        // 按钮放 FlowLayoutPanel 流式排布：AutoSize 按钮在构造期尺寸未知，
        // 手工按 save.Right 定位会读到默认宽度导致重叠；流式面板结构性保证互不重叠
        var save = new Button
        {
            Text = "保存并检测",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 4, 14, 4),
            Margin = new Padding(0),
        };
        var cancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 4, 14, 4),
            Margin = new Padding(14, 0, 0, 0),
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Location = new Point(102, 244),
            Margin = new Padding(0),
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        save.Click += OnSave;
        cancel.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            lblTitle, lblId, _studentId, lblPwd, _password, lblSvc, _service,
            _autoStart, _hint, buttons,
        });
        AcceptButton = save;
        CancelButton = cancel;

        LoadExisting();
    }

    private void LoadExisting()
    {
        var cfg = _store.Load();
        _studentId.Text = cfg.StudentId;
        var existing = ServiceTypes.All.FirstOrDefault(o => o.Suffix == cfg.ServiceSuffix);
        _service.SelectedIndex = existing != null ? Array.IndexOf(ServiceTypes.All, existing) : 1; // 默认中国联通
        _autoStart.Checked = cfg.AutoStart;
        _password.PlaceholderText = cfg.IsConfigured ? "（已保存，留空不修改）" : "统一身份认证密码";
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _errors.Clear();

        var studentId = _studentId.Text.Trim();
        if (studentId.Length == 0)
        {
            _errors.SetError(_studentId, "请输入学号");
            return;
        }
        if (!studentId.All(char.IsAsciiDigit))
        {
            // 学号全数字是该校惯例；非纯数字仅警告不阻断
            Log.Warn("学号非纯数字，按原样提交");
        }

        var password = _password.Text;
        var keepOld = password.Length == 0 && _store.Load().IsConfigured;
        if (!keepOld && password.Length == 0)
        {
            _errors.SetError(_password, "请输入密码");
            return;
        }
        if (password.Contains(';'))
        {
            _errors.SetError(_password, "门户限制：密码不能包含分号（;）");
            return;
        }

        var suffix = (_service.SelectedItem as ServiceTypes.Option)?.Suffix ?? "";
        if (keepOld)
        {
            var cfg = _store.Load();
            cfg.StudentId = studentId;
            cfg.ServiceSuffix = suffix;
            cfg.AutoStart = _autoStart.Checked;
            _store.Save(cfg);
        }
        else
        {
            _store.UpdateCredentials(studentId, password, suffix);
            var cfg = _store.Load();
            cfg.AutoStart = _autoStart.Checked;
            _store.Save(cfg);
        }

        if (AutoStartManager.SetEnabled(_autoStart.Checked) != _autoStart.Checked)
        {
            Log.Warn("自启动注册表写入未生效");
        }

        Log.Info($"配置已保存 (账号 {Log.Mask(string.IsNullOrEmpty(suffix) ? studentId : $"{studentId}@{suffix}")})");
        _coordinator.OnConfigSaved();
        Close();
    }
}
