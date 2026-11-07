using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Honor 9 HarmonyOS 2 Downloader")]
[assembly: AssemblyDescription("Downloads and prepares the Honor 9 HarmonyOS 2 installer")]
[assembly: AssemblyCompany("rAmka220")]
[assembly: AssemblyProduct("Honor 9 HarmonyOS 2 Downloader")]
[assembly: AssemblyVersion("1.1.0.0")]

namespace Honor9HarmonyDownloader
{
    internal sealed class ReleaseInfo
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public List<ReleaseAsset> assets { get; set; }
    }

    internal sealed class ReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public string digest { get; set; }
        public long size { get; set; }
    }

    internal sealed class MainForm : Form
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/rAmka220/Honor9-HarmonyOS2-Installer/releases/latest";

        private readonly HttpClient _http;
        private readonly TextBox _destination;
        private readonly RadioButton _unsigned;
        private readonly RadioButton _signed;
        private readonly Label _releaseLabel;
        private readonly Label _statusLabel;
        private readonly ProgressBar _progress;
        private readonly RichTextBox _log;
        private readonly Button _refreshButton;
        private readonly Button _downloadButton;
        private readonly Button _cancelButton;
        private readonly Button _checkButton;
        private readonly Button _installButton;
        private ReleaseInfo _release;
        private Dictionary<string, string> _checksums;
        private CancellationTokenSource _cancellation;
        private Process _operationProcess;
        private bool _prepared;
        private bool _phoneChecked;

        public MainForm()
        {
            Text = "Honor 9 — HarmonyOS 2 Installer";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(780, 620);
            Size = new Size(850, 680);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            _http.Timeout = Timeout.InfiniteTimeSpan;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Honor9-HarmonyOS2-Downloader/1.1");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 9;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label title = new Label();
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            title.Text = "Honor 9 — HarmonyOS 2.0.0.150";
            title.Margin = new Padding(0, 0, 0, 4);
            root.Controls.Add(title, 0, 0);

            Label warning = new Label();
            warning.AutoSize = true;
            warning.ForeColor = Color.DarkRed;
            warning.Text = "Требуются USERLOCK и FBLOCK: UNLOCKED. Данные телефона будут удалены. " +
                           "Bootloader после установки не блокировать.";
            warning.Margin = new Padding(0, 0, 0, 12);
            root.Controls.Add(warning, 0, 1);

            GroupBox destinationBox = new GroupBox();
            destinationBox.Text = "Папка установщика";
            destinationBox.Dock = DockStyle.Top;
            destinationBox.AutoSize = true;
            destinationBox.Padding = new Padding(9);
            TableLayoutPanel destinationLayout = new TableLayoutPanel();
            destinationLayout.Dock = DockStyle.Fill;
            destinationLayout.AutoSize = true;
            destinationLayout.ColumnCount = 2;
            destinationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            destinationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            destinationBox.Controls.Add(destinationLayout);
            _destination = new TextBox();
            _destination.Dock = DockStyle.Fill;
            _destination.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Honor9Harmony");
            _destination.TextChanged += delegate { RefreshPreparedState(); };
            Button browse = new Button();
            browse.Text = "Обзор...";
            browse.AutoSize = true;
            browse.Margin = new Padding(8, 0, 0, 0);
            browse.Click += BrowseClick;
            destinationLayout.Controls.Add(_destination, 0, 0);
            destinationLayout.Controls.Add(browse, 1, 0);
            root.Controls.Add(destinationBox, 0, 2);

            GroupBox preasBox = new GroupBox();
            preasBox.Text = "Вариант PREAS";
            preasBox.Dock = DockStyle.Top;
            preasBox.AutoSize = true;
            preasBox.Padding = new Padding(9);
            FlowLayoutPanel preasFlow = new FlowLayoutPanel();
            preasFlow.Dock = DockStyle.Fill;
            preasFlow.AutoSize = true;
            preasFlow.FlowDirection = FlowDirection.TopDown;
            preasFlow.WrapContents = false;
            _unsigned = new RadioButton();
            _unsigned.AutoSize = true;
            _unsigned.Checked = true;
            _unsigned.Text = "Изменённый неподписанный /china PREAS — проверен на STF-AL10";
            _signed = new RadioButton();
            _signed.AutoSize = true;
            _signed.Text = "Подписанный Huawei китайский PREAS — экспериментально с 2.0.0.150";
            _unsigned.CheckedChanged += delegate { if (_unsigned.Checked) RefreshPreparedState(); };
            _signed.CheckedChanged += delegate { if (_signed.Checked) RefreshPreparedState(); };
            preasFlow.Controls.Add(_unsigned);
            preasFlow.Controls.Add(_signed);
            preasBox.Controls.Add(preasFlow);
            root.Controls.Add(preasBox, 0, 3);

            FlowLayoutPanel releaseFlow = new FlowLayoutPanel();
            releaseFlow.Dock = DockStyle.Top;
            releaseFlow.AutoSize = true;
            releaseFlow.WrapContents = false;
            _refreshButton = new Button();
            _refreshButton.Text = "Проверить GitHub";
            _refreshButton.AutoSize = true;
            _refreshButton.Click += async delegate { await RefreshReleaseAsync(); };
            _releaseLabel = new Label();
            _releaseLabel.AutoSize = true;
            _releaseLabel.Text = "Release ещё не проверен";
            _releaseLabel.Margin = new Padding(12, 7, 0, 0);
            releaseFlow.Controls.Add(_refreshButton);
            releaseFlow.Controls.Add(_releaseLabel);
            root.Controls.Add(releaseFlow, 0, 4);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Top;
            actions.AutoSize = true;
            actions.WrapContents = true;
            _downloadButton = new Button();
            _downloadButton.Text = "1. Скачать и подготовить";
            _downloadButton.AutoSize = true;
            _downloadButton.Enabled = false;
            _downloadButton.Click += async delegate { await DownloadAndPrepareAsync(); };
            _cancelButton = new Button();
            _cancelButton.Text = "Отмена";
            _cancelButton.AutoSize = true;
            _cancelButton.Enabled = false;
            _cancelButton.Click += delegate { if (_cancellation != null) _cancellation.Cancel(); };
            _checkButton = new Button();
            _checkButton.Text = "2. Проверить телефон";
            _checkButton.AutoSize = true;
            _checkButton.Enabled = false;
            _checkButton.Click += async delegate { await RunPhoneCheckAsync(); };
            _installButton = new Button();
            _installButton.Text = "3. Прошить телефон";
            _installButton.AutoSize = true;
            _installButton.Enabled = false;
            _installButton.Click += async delegate { await RunInstallationAsync(); };
            actions.Controls.Add(_downloadButton);
            actions.Controls.Add(_cancelButton);
            actions.Controls.Add(_checkButton);
            actions.Controls.Add(_installButton);
            root.Controls.Add(actions, 0, 5);

            TableLayoutPanel progressLayout = new TableLayoutPanel();
            progressLayout.Dock = DockStyle.Top;
            progressLayout.AutoSize = true;
            progressLayout.ColumnCount = 1;
            _statusLabel = new Label();
            _statusLabel.AutoSize = true;
            _statusLabel.Text = "Готово к проверке Release";
            _progress = new ProgressBar();
            _progress.Dock = DockStyle.Top;
            _progress.Height = 22;
            _progress.Margin = new Padding(0, 5, 0, 8);
            progressLayout.Controls.Add(_statusLabel, 0, 0);
            progressLayout.Controls.Add(_progress, 0, 1);
            root.Controls.Add(progressLayout, 0, 6);

            _log = new RichTextBox();
            _log.Dock = DockStyle.Fill;
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = RichTextBoxScrollBars.Vertical;
            _log.BackColor = Color.White;
            _log.Font = new Font("Consolas", 9F);
            root.Controls.Add(_log, 0, 7);

            Label footer = new Label();
            footer.AutoSize = true;
            footer.Text = "Используйте пакет на свой страх и риск.";
            footer.ForeColor = Color.DarkRed;
            footer.Margin = new Padding(0, 8, 0, 0);
            root.Controls.Add(footer, 0, 8);

            Shown += async delegate { await RefreshReleaseAsync(); };
            FormClosing += MainFormClosing;
            RefreshPreparedState();
        }

        private void BrowseClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для установщика";
                dialog.SelectedPath = _destination.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _destination.Text = dialog.SelectedPath;
            }
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_operationProcess != null)
            {
                MessageBox.Show(this,
                    "Сейчас выполняется проверка или прошивка. Не закрывайте программу и не отключайте телефон.",
                    "Операция выполняется", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            if (_cancellation != null)
            {
                DialogResult answer = MessageBox.Show(this,
                    "Загрузка ещё выполняется. Остановить её? Частичные файлы сохранятся для продолжения.",
                    "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _cancellation.Cancel();
            }
        }

        private async Task RefreshReleaseAsync()
        {
            SetBusy(true, false);
            _statusLabel.Text = "Получение данных GitHub Release...";
            try
            {
                string json = await _http.GetStringAsync(LatestReleaseApi);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 4 * 1024 * 1024;
                _release = serializer.Deserialize<ReleaseInfo>(json);
                if (_release == null || _release.assets == null)
                    throw new InvalidDataException("GitHub вернул пустой список файлов");

                _checksums = await LoadChecksumsAsync(_release);
                ReleaseAsset signedAsset = FindSignedPreas(_release);
                _signed.Enabled = signedAsset != null;
                if (signedAsset == null)
                {
                    _signed.Checked = false;
                    _unsigned.Checked = true;
                    _signed.Text = "Подписанный Huawei PREAS — пока отсутствует в " + _release.tag_name;
                }
                else
                {
                    _signed.Text = "Подписанный Huawei китайский PREAS — экспериментально с 2.0.0.150";
                }

                _releaseLabel.Text = string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1} файлов", _release.tag_name, _release.assets.Count);
                Log("Release " + _release.tag_name + " получен.");
                Log(signedAsset == null
                    ? "Подписанный PREAS в этом Release отсутствует."
                    : "Подписанный PREAS доступен как отдельный файл.");
                _statusLabel.Text = "Выберите PREAS и нажмите «Скачать и подготовить»";
                _downloadButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _release = null;
                _downloadButton.Enabled = false;
                _statusLabel.Text = "Не удалось получить Release";
                Log("ОШИБКА: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Ошибка GitHub", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, false);
            }
        }

        private async Task<Dictionary<string, string>> LoadChecksumsAsync(ReleaseInfo release)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ReleaseAsset asset in release.assets)
            {
                if (!string.IsNullOrEmpty(asset.digest) &&
                    asset.digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    result[asset.name] = asset.digest.Substring(7).ToLowerInvariant();
            }

            ReleaseAsset sums = release.assets.FirstOrDefault(delegate(ReleaseAsset a)
            {
                return string.Equals(a.name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase);
            });
            if (sums != null)
            {
                string text = await _http.GetStringAsync(sums.browser_download_url);
                foreach (string line in text.Replace("\r", "").Split('\n'))
                {
                    Match match = Regex.Match(line, "^([0-9a-fA-F]{64})\\s+\\*?(.+)$");
                    if (match.Success)
                        result[match.Groups[2].Value.Trim()] = match.Groups[1].Value.ToLowerInvariant();
                }
            }
            return result;
        }

        private async Task DownloadAndPrepareAsync()
        {
            if (_release == null)
            {
                await RefreshReleaseAsync();
                if (_release == null) return;
            }

            string target;
            try
            {
                target = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_destination.Text.Trim()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Неверная папка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ReleaseAsset baseAsset = _release.assets.FirstOrDefault(delegate(ReleaseAsset a)
            {
                return a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                       a.name.IndexOf("Installer-base-", StringComparison.OrdinalIgnoreCase) >= 0;
            });
            List<ReleaseAsset> payloadParts = _release.assets.Where(delegate(ReleaseAsset a)
            {
                return Regex.IsMatch(a.name, "^payload\\.7z\\.part\\d+$", RegexOptions.IgnoreCase);
            }).OrderBy(delegate(ReleaseAsset a) { return a.name; }, StringComparer.OrdinalIgnoreCase).ToList();
            ReleaseAsset signedAsset = _signed.Checked ? FindSignedPreas(_release) : null;

            if (baseAsset == null || payloadParts.Count < 2)
            {
                MessageBox.Show(this, "В Release отсутствует base ZIP или части payload.",
                    "Неполный Release", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_signed.Checked && signedAsset == null)
            {
                MessageBox.Show(this, "Подписанный PREAS ещё не опубликован в выбранном Release.",
                    "Файл недоступен", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ReleaseAsset> downloads = new List<ReleaseAsset>();
            downloads.Add(baseAsset);
            downloads.AddRange(payloadParts);
            if (signedAsset != null) downloads.Add(signedAsset);
            foreach (ReleaseAsset asset in downloads)
            {
                if (!_checksums.ContainsKey(asset.name))
                {
                    MessageBox.Show(this, "Для файла отсутствует SHA-256: " + asset.name,
                        "Небезопасный Release", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            Directory.CreateDirectory(target);
            string cache = Path.Combine(target, ".downloads");
            Directory.CreateDirectory(cache);
            _cancellation = new CancellationTokenSource();
            _phoneChecked = false;
            SetBusy(true, true);
            _progress.Value = 0;

            long total = downloads.Sum(delegate(ReleaseAsset a) { return a.size; });
            long completed = 0;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                foreach (ReleaseAsset asset in downloads)
                {
                    long baseCompleted = completed;
                    Progress<long> itemProgress = new Progress<long>(delegate(long value)
                    {
                        long all = Math.Min(total, baseCompleted + value);
                        int percent = total == 0 ? 0 : (int)Math.Min(100, all * 100L / total);
                        _progress.Value = percent;
                        double speed = watch.Elapsed.TotalSeconds < 0.2 ? 0 : all / watch.Elapsed.TotalSeconds;
                        _statusLabel.Text = string.Format(CultureInfo.InvariantCulture,
                            "{0}: {1:F2}/{2:F2} ГиБ — {3:F1} МиБ/с",
                            asset.name, all / 1073741824.0, total / 1073741824.0, speed / 1048576.0);
                    });
                    await DownloadAssetAsync(asset, cache, itemProgress, _cancellation.Token);
                    completed += asset.size;
                }

                _statusLabel.Text = "Распаковка базового установщика...";
                string basePath = Path.Combine(cache, baseAsset.name);
                ExtractZipSafely(basePath, target);

                _statusLabel.Text = "Сборка payload.7z...";
                await AssemblePayloadAsync(payloadParts, cache, target, _cancellation.Token);

                if (signedAsset != null)
                {
                    string signedSource = Path.Combine(cache, signedAsset.name);
                    string signedTarget = Path.Combine(target, signedAsset.name);
                    File.Copy(signedSource, signedTarget, true);
                    File.WriteAllText(signedTarget + ".sha256",
                        _checksums[signedAsset.name] + " *" + signedAsset.name + Environment.NewLine,
                        Encoding.ASCII);
                }

                foreach (ReleaseAsset part in payloadParts)
                    TryDelete(Path.Combine(cache, part.name));
                TryDelete(basePath);
                if (signedAsset != null) TryDelete(Path.Combine(cache, signedAsset.name));
                try { if (!Directory.EnumerateFileSystemEntries(cache).Any()) Directory.Delete(cache); }
                catch { }

                _progress.Value = 100;
                _prepared = true;
                _phoneChecked = false;
                _statusLabel.Text = "КОМПЛЕКТ ГОТОВ: " + target;
                Log("Комплект подготовлен: " + target);
                Log("PREAS: " + (_signed.Checked ? "Signed" : "Unsigned"));
                MessageBox.Show(this,
                    "Файлы скачаны, SHA-256 проверены, payload подготовлен.\r\n\r\n" +
                    "Сначала нажмите «Проверить телефон».",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                _statusLabel.Text = "Загрузка остановлена; её можно продолжить позже";
                Log("Операция отменена. Частичные файлы сохранены.");
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Ошибка подготовки";
                Log("ОШИБКА: " + ex);
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cancellation.Dispose();
                _cancellation = null;
                SetBusy(false, false);
            }
        }

        private async Task DownloadAssetAsync(ReleaseAsset asset, string directory,
            IProgress<long> progress, CancellationToken cancellationToken)
        {
            string finalPath = Path.Combine(directory, asset.name);
            string partialPath = finalPath + ".download";
            string expected = _checksums[asset.name];

            if (File.Exists(finalPath))
            {
                _statusLabel.Text = "Проверка существующего " + asset.name;
                if (await VerifyHashAsync(finalPath, expected, cancellationToken))
                {
                    progress.Report(asset.size);
                    Log(asset.name + " уже загружен и проверен.");
                    return;
                }
                TryDelete(finalPath);
            }

            long existing = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
            if (existing > asset.size)
            {
                TryDelete(partialPath);
                existing = 0;
            }

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, asset.browser_download_url))
            {
                if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
                using (HttpResponseMessage response = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        existing = 0;
                        TryDelete(partialPath);
                    }
                    response.EnsureSuccessStatusCode();
                    FileMode mode = existing > 0 ? FileMode.Append : FileMode.Create;
                    using (Stream input = await response.Content.ReadAsStreamAsync())
                    using (FileStream output = new FileStream(partialPath, mode, FileAccess.Write,
                        FileShare.None, 1024 * 1024, true))
                    {
                        byte[] buffer = new byte[1024 * 1024];
                        long current = existing;
                        progress.Report(current);
                        while (true)
                        {
                            int read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            if (read == 0) break;
                            await output.WriteAsync(buffer, 0, read, cancellationToken);
                            current += read;
                            progress.Report(current);
                        }
                    }
                }
            }

            _statusLabel.Text = "Проверка SHA-256: " + asset.name;
            if (!await VerifyHashAsync(partialPath, expected, cancellationToken))
                throw new InvalidDataException("SHA-256 не совпадает: " + asset.name);
            TryDelete(finalPath);
            File.Move(partialPath, finalPath);
            Log(asset.name + " скачан и проверен.");
        }

        private async Task AssemblePayloadAsync(List<ReleaseAsset> parts, string cache,
            string target, CancellationToken cancellationToken)
        {
            string output = Path.Combine(target, "payload.7z");
            string temporary = output + ".tmp";
            string expected = ReadSingleHash(Path.Combine(target, "payload.7z.sha256"), "payload.7z");

            if (File.Exists(output) && await VerifyHashAsync(output, expected, cancellationToken))
            {
                Log("Существующий payload.7z уже готов.");
                return;
            }
            TryDelete(output);
            TryDelete(temporary);

            using (FileStream destination = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                byte[] buffer = new byte[1024 * 1024];
                foreach (ReleaseAsset part in parts)
                {
                    using (FileStream source = new FileStream(Path.Combine(cache, part.name), FileMode.Open,
                        FileAccess.Read, FileShare.Read, 1024 * 1024, true))
                    {
                        while (true)
                        {
                            int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            if (read == 0) break;
                            await destination.WriteAsync(buffer, 0, read, cancellationToken);
                        }
                    }
                }
            }

            _statusLabel.Text = "Финальная проверка SHA-256 payload.7z...";
            if (!await VerifyHashAsync(temporary, expected, cancellationToken))
                throw new InvalidDataException("SHA-256 собранного payload.7z не совпадает");
            File.Move(temporary, output);
            Log("payload.7z собран и проверен.");
        }

        private static string ReadSingleHash(string path, string expectedName)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Не найден файл хеша", path);
            foreach (string line in File.ReadAllLines(path))
            {
                Match match = Regex.Match(line, "^([0-9a-fA-F]{64})\\s+\\*?(.+)$");
                if (match.Success && string.Equals(match.Groups[2].Value.Trim(), expectedName,
                    StringComparison.OrdinalIgnoreCase))
                    return match.Groups[1].Value.ToLowerInvariant();
            }
            throw new InvalidDataException("Не найден SHA-256 для " + expectedName);
        }

        private static Task<bool> VerifyHashAsync(string path, string expected,
            CancellationToken cancellationToken)
        {
            return Task.Run(delegate
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    cancellationToken.ThrowIfCancellationRequested();
                    return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                }
            }, cancellationToken);
        }

        private static void ExtractZipSafely(string zipPath, string destination)
        {
            string root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string path = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Опасный путь в ZIP: " + entry.FullName);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                        entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        string parent = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        entry.ExtractToFile(path, true);
                    }
                }
            }
        }

        private async Task RunPhoneCheckAsync()
        {
            string root = GetPreparedRoot();
            if (root == null) return;
            string script = Path.Combine(root, "check-phone.cmd");
            if (!File.Exists(script))
            {
                MessageBox.Show(this, "Сначала скачайте и подготовьте комплект.",
                    "Файл не найден", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _log.Clear();
            _phoneChecked = false;
            Log("Проверка телефона: модель, USERLOCK, FBLOCK и версия системы.");
            int code = await RunPowerShellAsync(root, Path.Combine(root, "install.ps1"), "-CheckOnly", null);
            if (code == 0)
            {
                _phoneChecked = true;
                _statusLabel.Text = "CHECK PASSED — телефон готов к установке";
                MessageBox.Show(this, "Проверка пройдена. Разделы телефона не изменялись.",
                    "CHECK PASSED", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _phoneChecked = false;
                _statusLabel.Text = "Проверка телефона завершилась ошибкой";
                MessageBox.Show(this, "Проверка не пройдена. Исправьте ошибку из журнала; прошивку не запускайте.",
                    "CHECK FAILED", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshPreparedState();
        }

        private async Task RunInstallationAsync()
        {
            string root = GetPreparedRoot();
            if (root == null) return;
            string script = Path.Combine(root, "install.ps1");
            string payload = Path.Combine(root, "payload.7z");
            if (!File.Exists(script) || !File.Exists(payload))
            {
                MessageBox.Show(this, "Сначала скачайте и подготовьте комплект.",
                    "Файлы не найдены", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string mode = _signed.Checked ? "Signed" : "Unsigned";
            if (mode == "Signed" && !File.Exists(Path.Combine(root, "PREAS-signed-Huawei-140-sparse.img")))
            {
                MessageBox.Show(this, "Файл подписанного PREAS отсутствует. Скачайте комплект заново " +
                    "с выбранным подписанным PREAS.", "PREAS не найден",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (EraseConfirmForm confirmation = new EraseConfirmForm(mode))
            {
                if (confirmation.ShowDialog(this) != DialogResult.OK) return;
            }

            string scriptText = File.ReadAllText(script);
            bool supportsPreasMode = scriptText.IndexOf("$PreasMode", StringComparison.OrdinalIgnoreCase) >= 0;
            if (mode == "Signed" && !supportsPreasMode)
            {
                MessageBox.Show(this,
                    "Скачанный base-пакет слишком старый и не поддерживает выбор подписанного PREAS. " +
                    "Обновите Release через программу.", "Требуется новый Release",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _log.Clear();
            Log("НАЧАЛО УСТАНОВКИ. PREAS: " + mode + ".");
            Log("Не отключайте USB и не закрывайте программу до результата DONE.");
            string arguments = supportsPreasMode ? "-PreasMode " + mode : "";
            int code = await RunPowerShellAsync(root, script, arguments, "STF-HARMONY-ERASE");
            if (code == 0)
            {
                _statusLabel.Text = "DONE — установка завершена";
                MessageBox.Show(this,
                    "Установка завершена. Первая загрузка может занять 10–15 минут. " +
                    "Bootloader не блокировать.", "DONE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _statusLabel.Text = "Установка остановлена с ошибкой";
                MessageBox.Show(this,
                    "Установка остановлена. Не выполняйте relock. Сохраните папки logs и backups.",
                    "ОШИБКА УСТАНОВКИ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task<int> RunPowerShellAsync(string root, string script, string scriptArguments,
            string standardInput)
        {
            if (!File.Exists(script))
                throw new FileNotFoundException("Не найден сценарий установщика", script);

            SetBusy(true, false);
            _statusLabel.Text = standardInput == null ? "Проверка телефона..." : "Выполняется прошивка...";
            Process process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " +
                                          scriptArguments;
            process.StartInfo.WorkingDirectory = root;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) AppendProcessLine(e.Data, false);
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) AppendProcessLine(e.Data, true);
            };

            try
            {
                _operationProcess = process;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (standardInput != null)
                {
                    process.StandardInput.WriteLine(standardInput);
                    process.StandardInput.Flush();
                }
                process.StandardInput.Close();
                await Task.Run(delegate { process.WaitForExit(); });
                process.WaitForExit();
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                AppendProcessLine("Не удалось запустить PowerShell: " + ex.Message, true);
                return -1;
            }
            finally
            {
                _operationProcess = null;
                process.Dispose();
                SetBusy(false, false);
            }
        }

        private void AppendProcessLine(string line, bool error)
        {
            if (IsDisposed) return;
            BeginInvoke((MethodInvoker)delegate
            {
                _log.SelectionStart = _log.TextLength;
                _log.SelectionColor = error ? Color.DarkRed : Color.Black;
                _log.AppendText(line + Environment.NewLine);
                _log.SelectionColor = Color.Black;
                _log.ScrollToCaret();
            });
        }

        private string GetPreparedRoot()
        {
            try { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_destination.Text.Trim())); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Неверная папка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private void RefreshPreparedState()
        {
            if (_cancellation != null || _operationProcess != null) return;
            string root;
            try { root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_destination.Text.Trim())); }
            catch
            {
                _prepared = false;
                _phoneChecked = false;
                if (_checkButton != null) _checkButton.Enabled = false;
                if (_installButton != null) _installButton.Enabled = false;
                return;
            }
            bool filesReady = File.Exists(Path.Combine(root, "install.ps1")) &&
                              File.Exists(Path.Combine(root, "payload.7z"));
            if (_signed != null && _signed.Checked)
                filesReady = filesReady && File.Exists(Path.Combine(root, "PREAS-signed-Huawei-140-sparse.img"));
            if (!filesReady) _phoneChecked = false;
            _prepared = filesReady;
            if (_checkButton != null) _checkButton.Enabled = _prepared;
            if (_installButton != null) _installButton.Enabled = _prepared && _phoneChecked;
        }

        private static ReleaseAsset FindSignedPreas(ReleaseInfo release)
        {
            if (release == null || release.assets == null) return null;
            return release.assets.FirstOrDefault(delegate(ReleaseAsset a)
            {
                return a.name.StartsWith("PREAS-signed-Huawei-140-sparse", StringComparison.OrdinalIgnoreCase) &&
                       a.name.EndsWith(".img", StringComparison.OrdinalIgnoreCase);
            });
        }

        private void SetBusy(bool busy, bool cancellable)
        {
            _refreshButton.Enabled = !busy;
            _downloadButton.Enabled = !busy && _release != null;
            _destination.Enabled = !busy;
            _unsigned.Enabled = !busy;
            _signed.Enabled = !busy && FindSignedPreas(_release) != null;
            _cancelButton.Enabled = busy && cancellable;
            _checkButton.Enabled = !busy && _prepared;
            _installButton.Enabled = !busy && _prepared && _phoneChecked;
        }

        private void Log(string text)
        {
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " +
                            text + Environment.NewLine);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    internal sealed class EraseConfirmForm : Form
    {
        private readonly TextBox _confirmation;
        private readonly Button _continue;

        public EraseConfirmForm(string preasMode)
        {
            Text = "Подтверждение прошивки";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 230);
            Font = new Font("Segoe UI", 9F);

            Label warning = new Label();
            warning.Location = new Point(16, 15);
            warning.Size = new Size(528, 86);
            warning.ForeColor = Color.DarkRed;
            warning.Text = "ВНИМАНИЕ: внутренняя память телефона будет полностью удалена.\r\n" +
                           "PREAS: " + preasMode + ". Bootloader после установки блокировать нельзя.\r\n\r\n" +
                           "Для продолжения введите точно: STF-HARMONY-ERASE";
            Controls.Add(warning);

            _confirmation = new TextBox();
            _confirmation.Location = new Point(16, 112);
            _confirmation.Size = new Size(528, 25);
            _confirmation.TextChanged += delegate
            {
                _continue.Enabled = string.Equals(_confirmation.Text, "STF-HARMONY-ERASE", StringComparison.Ordinal);
            };
            Controls.Add(_confirmation);

            _continue = new Button();
            _continue.Text = "Начать прошивку";
            _continue.Location = new Point(314, 169);
            _continue.Size = new Size(120, 32);
            _continue.Enabled = false;
            _continue.DialogResult = DialogResult.OK;
            Controls.Add(_continue);

            Button cancel = new Button();
            cancel.Text = "Отмена";
            cancel.Location = new Point(444, 169);
            cancel.Size = new Size(100, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = _continue;
            CancelButton = cancel;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
