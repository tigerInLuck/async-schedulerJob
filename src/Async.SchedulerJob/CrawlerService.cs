using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Renci.SshNet;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Async.SchedulerJob;

/// <summary>
/// Represents the crawler to collect the data of spectro daily in laboratory device.
/// </summary>
public class CrawlerService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlerService"/> class.
    /// </summary>
    public CrawlerService(ILogger logger)
    {
        // Get logger
        _logger = logger;

        // Task mapping
        _taskAndLastRunTimeMap = new ConcurrentDictionary<Guid, DateTime>();
        _taskAndCancellationMap = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        // Set the CrawlerService to SchedulerJob
        SchedulerJob.CrawlerService = this;

        // Create Scheduler
        StdSchedulerFactory factory = new();
        _scheduler = factory.GetScheduler().GetAwaiter().GetResult();
        _scheduler.Start().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Start a scheduler for devices.
    /// </summary>
    /// <returns></returns>
    public async Task StartAsync(List<DeviceInfoEntity> deviceEntities)
    {
        foreach (DeviceInfoEntity deviceEntity in deviceEntities)
        {
            await CreateTaskAsync(deviceEntity);
            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Create a task.
    /// </summary>
    private async Task CreateTaskAsync(DeviceInfoEntity device)
    {
        IJobDetail jobDetail = JobBuilder.Create<SchedulerJob>()
            .UsingJobData(new JobDataMap
            {
                new("DeviceId", device.Di_Id.ToString()),
                new("DeviceIp", device.Di_DeviceIp),
                new("Ip", device.Di_HostIp),
                new("Port", device.Di_HostPort),
                new("UserName", device.Di_HostUserName),
                new("Password", device.Di_HostPassword)
            })
            .WithIdentity(device.Di_Id.ToString())
            .Build();

        ITrigger trigger = TriggerBuilder.Create()
            .WithSimpleSchedule(m =>
            {
                m.WithIntervalInSeconds(device.Di_ScanInterval).RepeatForever();
            })
            .Build();

        // Add job and trigger into the scheduler with cancellation token source
        CancellationTokenSource cancellationTokenSource = new();

        // log last run time
        if (!_taskAndLastRunTimeMap.ContainsKey(device.Di_Id))
            _taskAndLastRunTimeMap.TryAdd(device.Di_Id, (DateTime) TimeHelper.UtcToLocalTime(DateTime.UtcNow));
        else
            _taskAndLastRunTimeMap[device.Di_Id] = TimeHelper.UtcToLocalTime(DateTime.UtcNow);

        // set task and cancellationTokenSource
        if (!_taskAndCancellationMap.ContainsKey(device.Di_Id))
            _taskAndCancellationMap.TryAdd(device.Di_Id, cancellationTokenSource);
        else
            _taskAndCancellationMap[device.Di_Id] = cancellationTokenSource;

        // Set Schedule Job
        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationTokenSource.Token);

        // Watch task
        await WatchTaskAsync(device, cancellationTokenSource);
        _logger.LogInformation("[DeviceId]{deviceId} the task is created.", device.Di_Id);
    }

    /// <summary>
    /// Watch and cancel the task if something wrong.
    /// </summary>
    private Task WatchTaskAsync(DeviceInfoEntity device, CancellationTokenSource cancellationTokenSource)
    {
        int timeOut = device.Di_ScanInterval / 60 + 10;
        Task.Run(async () =>
        {
            bool isRestartSelf = false;
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(1000);
                if ((TimeHelper.UtcToLocalTime(DateTime.UtcNow) - _taskAndLastRunTimeMap[device.Di_Id]).Minutes <= timeOut)
                    continue;

                isRestartSelf = true;
                cancellationTokenSource.Cancel();
            }

            _logger.LogInformation("[DeviceId]{deviceId} 'IsCancellationRequested': {status}", device.Di_Id, cancellationTokenSource.IsCancellationRequested);

            // Try delete current task first if still running
            if (!await _scheduler.DeleteJob(new JobKey(device.Di_Id.ToString())))
                await _scheduler.DeleteJob(new JobKey(device.Di_Id.ToString()));

            // Restart
            if (isRestartSelf)
            {
                await CreateTaskAsync(device);
                _logger.LogInformation("[DeviceId]{deviceId} the task job has been restarted by 'CancellationTokenSource', because it has something wrongs.", device.Di_Id);
            }
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Add a new device task into the scheduler.
    /// </summary>
    public async Task AddScheduleTask(DeviceInfoEntity device)
    {
        await CreateTaskAsync(device);
    }

    /// <summary>
    /// Stop a device task from the scheduler.
    /// </summary>
    public Task StopScheduleTask(Guid deviceId)
    {
        if (!_taskAndCancellationMap.ContainsKey(deviceId))
            return Task.CompletedTask;

        // cancel
        _taskAndCancellationMap[deviceId].Cancel();
        _logger.LogInformation("[DeviceId]{deviceId} the task job has been stopped by API.", deviceId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Restart the device task.
    /// </summary>
    public async Task RestartScheduleTask(DeviceInfoEntity device)
    {
        if (!_taskAndCancellationMap.ContainsKey(device.Di_Id))
        {
            await CreateTaskAsync(device);
            return;
        }

        // cancel
        _taskAndCancellationMap[device.Di_Id].Cancel();
        JobKey jobKey = new(device.Di_Id.ToString());
        while (await _scheduler.CheckExists(jobKey))
            await Task.Delay(500);

        // restart
        await CreateTaskAsync(device);
        _logger.LogInformation("[DeviceId]{deviceId} the task job has been restarted by API.", device.Di_Id);
    }

    /// <summary>
    /// Execute the scheduler task for device.
    /// </summary>
    public async Task Execute(string deviceIdStr, string deviceIp, string ip, int port, string userName, string passWord)
    {
        _logger.LogInformation("[DeviceId]{deviceId} the task job is starting...", deviceIdStr);
        Guid deviceId = Guid.Parse(deviceIdStr);
        SshClient? sshClient = null;

        try
        {
            // connect to ssh host
            sshClient = new SshClient(ip, port, userName, passWord);
            sshClient.Connect();
            _logger.LogInformation("[DeviceId]{deviceId} Connected to {ip}:{port}", deviceIdStr, ip, port);

            // fetch data
            string wGet = string.Format(DAILY_URL, deviceIp);
            List<SpectroDailyEntity> spectroDailyList = await FetchDailyAsync(sshClient, deviceId, deviceIp, wGet);
            _logger.LogInformation("[DeviceId]{deviceId} fetch dailies data count: {count}", deviceIdStr, spectroDailyList.Count);

            // save data
            if (spectroDailyList is { Count: > 0 })
            {
                foreach (SpectroDailyEntity spectroDaily in spectroDailyList)
                {
                    List<SpectroDetailEntity> spectroDetailList = await FetchDetailsAsync(spectroDaily, sshClient, deviceId, deviceIp);
                    _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} fetch details data count: {count}", deviceIdStr, spectroDaily.Sd_Id, spectroDetailList.Count);

                    // save details
                    if (spectroDetailList is { Count: > 0 })
                        _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} saved details data", deviceIdStr, spectroDaily.Sd_Id);
                }
            }
            _logger.LogInformation("[DeviceId]{deviceId} the task job has been done!", deviceIdStr);
        }
        catch (Exception ex)
        {
            _logger.LogError("[DeviceId]{deviceId} [HostIp]{hostIp} [DeviceIp]{deviceIp} has exception: {ex}", deviceIdStr, ip, deviceIp, $"{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // Dispose ssh
            if (sshClient != null)
            {
                sshClient.Disconnect();
                sshClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Fetch daily data.
    /// </summary>
    async Task<List<SpectroDailyEntity>> FetchDailyAsync(SshClient sshClient, Guid deviceId, string deviceIp, string wGet)
    {
        List<SpectroDailyEntity> dailyEntities = new();
        List<SpectroDailyEntity> traceBackExistEntities = new();
        
        // mock config
        DateTime? traceMaxDate = null;
        DateTime? maxDate = DateTime.UtcNow;
        TraceBackTimeConfig traceBackTime = null;
        if (traceBackTime == null)
        {
            traceBackTime = new TraceBackTimeConfig { Minutes = 0, Hours = 0, Days = -7 };
            _logger.LogInformation("[DeviceId]{deviceId} [DeviceIp]{deviceIp} get config is empty. Set the default trace back time is 7 days ago.", deviceId, deviceIp);
        }

        if (traceBackTime.Minutes != 0)
            traceMaxDate = maxDate.Value.AddMinutes(traceBackTime.Minutes);
        else if (traceBackTime.Hours != 0)
            traceMaxDate = maxDate.Value.AddHours(traceBackTime.Hours);
        else if (traceBackTime.Days != 0)
            traceMaxDate = maxDate.Value.AddDays(traceBackTime.Days);
        // traceBackExistEntities =  await mySql.Context.Set<SpectroDailyEntity>().Where(p => p.Sd_DeviceId == deviceId && p.Sd_BizDateTime > traceMaxDate).ToListAsync();

        // wget url
        SshCommand? cmd = null;
        await Task.Run(() => { cmd = sshClient.RunCommand(wGet); }).WaitAsync(TimeSpan.FromMinutes(1.5));
        if (cmd == null)
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, "In Task.Run.Thread: the operation timed out within 1.5 minutes.");
            return dailyEntities;
        }
        if (cmd.ExitStatus != 0 || string.IsNullOrWhiteSpace(cmd.Result))
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, cmd.Error);
        }
        else
        {
            // parse html
            _logger.LogInformation("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, successfully!", deviceId, deviceIp, wGet);
            foreach (Match tr in Regex.Matches(cmd.Result, TR_PATTERN).Cast<Match>())
            {
                if (!tr.Value.Contains("href")) continue;
                if (!tr.Success) continue;
                int columnIndex = 1;
                SpectroDailyEntity spectroDaily = new() { Sd_DeviceId = deviceId };
                foreach (Match td in Regex.Matches(tr.Value, TD_PATTERN).Cast<Match>())
                {
                    switch (columnIndex)
                    {
                        case 1:
                            if (td.Value.Contains("href"))
                                spectroDaily.Sd_Url = Regex.Match(td.Value, HREF_PATTERN).Groups[1].Value.Trim().Replace("./", "/");
                            break;
                        case 2:
                            string dt = td.Groups[1].Value.Trim(); // 22/08/03 15:09
                            if (dt.IndexOf('/') == 2) dt = $"20{dt}";
                            spectroDaily.Sd_BizDateTime = DateTime.Parse(dt);
                            break;
                        case 3:
                            spectroDaily.Sd_Mode = td.Groups[1].Value.Trim();
                            break;
                        case 4:
                            spectroDaily.Sd_Item = td.Groups[1].Value.Trim();
                            break;
                    }

                    columnIndex++;
                }
                
                // find the trace entity if exist
                SpectroDailyEntity traceBackDailyEntity = traceBackExistEntities.Find(p => p.Sd_DeviceId == deviceId && p.Sd_BizDateTime == spectroDaily.Sd_BizDateTime);
                if (traceBackDailyEntity == null)
                {
                    // add a new daily data
                    if (maxDate is not null && !(spectroDaily.Sd_BizDateTime > maxDate)) continue;
                    dailyEntities.Add(spectroDaily);
                }
                else
                {
                    // set current url
                    traceBackDailyEntity.Sd_Url = spectroDaily.Sd_Url;
                    List<SpectroDetailEntity> traceBackDetailEntities = await FetchDetailsAsync(traceBackDailyEntity, sshClient, deviceId, deviceIp);
                    int? maxSeqNo = null;//await mySql.Context.Set<SpectroDetailEntity>().Where(p => p.Sd_DailyId == traceBackDailyEntity.Sd_Id).MaxAsync(p => (int?)p.Sd_SeqNo);
                    if (maxSeqNo is not null)
                    {
                        // supply the newest details data for this daily data
                        List<SpectroDetailEntity> supplyDetailEntities = traceBackDetailEntities.FindAll(p => p.Sd_SeqNo > maxSeqNo);
                        if (supplyDetailEntities is not { Count: > 0 }) continue;
                        await SupplyDetailsAsync(traceBackDailyEntity.Sd_Id, deviceId, supplyDetailEntities);
                        _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} saved details data for supplementary! count: {count}", deviceId, spectroDaily.Sd_Id, supplyDetailEntities.Count);
                    }
                    else
                    {
                        // supply the all detail data for Re-crawler
                        await SupplyDetailsAsync(traceBackDailyEntity.Sd_Id, deviceId, traceBackDetailEntities);
                        _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} saved details data for Re-crawler supplementary! count: {count}", deviceId, spectroDaily.Sd_Id, traceBackDetailEntities.Count);
                    }
                }

                spectroDaily.Sd_CreateDate = TimeHelper.UtcToLocalTime(DateTime.UtcNow);
                dailyEntities.Add(spectroDaily);
            }
        }

        // dispose
        cmd.Dispose();

        return dailyEntities;
    }

    /// <summary>
    /// Fetch details data.
    /// </summary>
    async Task<List<SpectroDetailEntity>> FetchDetailsAsync(SpectroDailyEntity spectroDaily, SshClient sshClient, Guid deviceId, string deviceIp)
    {
        List<SpectroDetailEntity> spectroDetailList = new();

        // wget url
        string wGet = string.Format(DETAIL_URL, deviceIp, spectroDaily.Sd_Url);
        SshCommand? cmd = null;
        await Task.Run(() => { cmd = sshClient.RunCommand(wGet); }).WaitAsync(TimeSpan.FromMinutes(1.5));
        if (cmd == null)
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, "In Task.Run.Thread: the operation timed out within 1.5 minutes.");
            return spectroDetailList;
        }
        if (cmd.ExitStatus != 0 || string.IsNullOrWhiteSpace(cmd.Result))
        {
            _logger.LogError("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, error: {error}", deviceId, deviceIp, wGet, cmd.Error);
        }
        else
        {
            // parse html
            _logger.LogInformation("[DeviceId]{deviceId} [DeviceIp]{deviceIp} executed command: {wGet}, successfully!", deviceId, deviceIp, wGet);
            foreach (Match tr in Regex.Matches(cmd.Result, TR_PATTERN).Cast<Match>())
            {
                string head = tr.Value.ToLower();
                if (head.Contains("no.") || head.Contains("kind")) continue;
                if (!tr.Success) continue;
                int columnIndex = 1;
                SpectroDetailEntity spectroDetail = new SpectroDetailEntity { Sd_DailyId = spectroDaily.Sd_Id, Sd_CreateDate = TimeHelper.UtcToLocalTime(DateTime.UtcNow) };
                foreach (Match td in Regex.Matches(tr.Value, TD_PATTERN).Cast<Match>())
                {
                    switch (columnIndex)
                    {
                        case 1:
                            spectroDetail.Sd_SeqNo = int.Parse(td.Groups[1].Value.Trim());
                            break;
                        case 2:
                            spectroDetail.Sd_Kind = td.Groups[1].Value.Trim();
                            break;
                        case 3:
                            spectroDetail.Sd_IDString = td.Groups[1].Value.Trim().Replace("&nbsp;", "");
                            break;
                        case 4:
                            spectroDetail.Sd_Percent = td.Groups[1].Value.Trim();
                            break;
                    }

                    columnIndex++;
                }

                spectroDetailList.Add(spectroDetail);
            }
        }

        // dispose
        cmd.Dispose();

        return spectroDetailList;
    }
    
    /// <summary>
    /// Supply details data.
    /// </summary>
    async Task SupplyDetailsAsync(Guid dailyId, Guid deviceId, List<SpectroDetailEntity> supplyDetailEntities)
    {
        try
        {
            // begin transaction for a batch
            // transaction = await mySql.Context.Database.BeginTransactionAsync();
            // await mySql.Context.Set<SpectroDetailEntity>().AddRangeAsync(supplyDetailEntities);

            // save db
            // await mySql.SaveChangesAsync();
            // await mySql.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInformation("[DeviceId]{deviceId} [DailyId]{dailyId} saved details data for supplementary failed, count: {count}, error: {error}", deviceId, dailyId, supplyDetailEntities.Count, ex.StackTrace);

            // Rollback db
            // if (transaction != null)
            //     await transaction.RollbackAsync();
        }
        finally
        {
            // if (transaction != null)
            //     await transaction.DisposeAsync();
        }
    }

    readonly ILogger _logger;
    readonly IScheduler _scheduler;
    readonly ConcurrentDictionary<Guid, DateTime> _taskAndLastRunTimeMap;
    readonly ConcurrentDictionary<Guid, CancellationTokenSource> _taskAndCancellationMap;

    const string TR_PATTERN = @"<tr[^>]*>(.*?)<\/tr>";
    const string TD_PATTERN = @"<td[^>]*>(.*?)<\/td>";
    const string HREF_PATTERN = "<a.+?href=\"(.+?)\".*>(.+)</a>";

    const string DAILY_URL = "wget -q -O - http://{0}/cgi/list.cgi?lang=1";
    const string DETAIL_URL = "wget -q -O - http://{0}/cgi{1}";
}