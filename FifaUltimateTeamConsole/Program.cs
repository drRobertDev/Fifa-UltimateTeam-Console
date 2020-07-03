using FifaUltimateTeamConsole.Utils;
using FutManagerLibrary;
using FutManagerLibrary.Configs;
using FutManagerLibrary.Exceptions;
using FutManagerLibrary.Interfaces;
using FutManagerLibrary.PublicModels;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/* If you build a new project rember to include deps requested by Library from Nuget
    <PackageReference Include="AngleSharp" Version="0.14.0" />
    <PackageReference Include="AngleSharp.Js" Version="0.14.0" />
    <PackageReference Include="BrotliSharpLib" Version="0.3.3" />
    <PackageReference Include="MailKit" Version="2.7.0" />
    <PackageReference Include="protobuf-net" Version="3.0.2" />
    <PackageReference Include="System.Collections.Concurrent" Version="4.3.0" />
    <PackageReference Include="System.Drawing.Common" Version="4.7.0" />
    <PackageReference Include="System.Management" Version="4.7.0" />
    <PackageReference Include="System.Net.Http.Json" Version="3.2.1" />
    <PackageReference Include="System.Security.Cryptography.Cng" Version="4.7.0" />
 */

namespace FifaUltimateTeamConsole
{
    class Program
    {
        static public string AssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static Logger _logger;

        private static DateTime NextTradePileCheck = DateTime.UtcNow.AddMinutes(5);
        private static DateTime NextWatchListCheck = DateTime.UtcNow.AddMinutes(5);
        private static DateTime NextUnassignedCheck = DateTime.UtcNow.AddMinutes(5);

        private static uint Coins;
        private static List<FifaAuction> TradePile = new List<FifaAuction>();
        private static List<FifaAuctionRestricted> WatchList = new List<FifaAuctionRestricted>();
        private static List<FifaItem> Unassigned = new List<FifaItem>();

        static async Task Main(string[] args)
        {
            //Avoid issue with libraries
            EncodingProvider provider = CodePagesEncodingProvider.Instance;
            Encoding.RegisterProvider(provider);

            _logger = new Logger();

            //Must call it first time or return errors other calls
            //Use FbmLibraryUi to gen user key-pass
            await FutManager.Init(_logger, Guid.Parse("6a17f074-2836-4ec3-9540-f5253205735f"), "7oxF51lNya+1yom4mjiNuQ==");
            _logger.Log("Main", "Current Tokens left " + FutManager.TokenLeft);

            await LoadGenInvDb();
            await ProcessAccount();

            Console.WriteLine("End");
            Console.ReadKey();
        }

        private static async Task ProcessAccount()
        {
            if (!await FutManager.IsFifaSupported(_logger))
                throw new NotImplementedException("New Fifa Code");

            IFutClient futClient = FutManager.CreateNewInstance(_logger);
            int errorTrigger = 0;
            while (true)
            {
                //Usually all main exception should be catch here ... specific exception like deny when bidding or stuck should be catch in specific method
                try
                {
                    await WorksProcess(futClient);
                    errorTrigger = 0;
                }
                catch (FifaConfigException ex)
                {
                    //All exceptions about wrong config like password ...
                    string message = "Config Exception: " + ex.Message;
                    _logger.LogError("Main", message);
                    break;
                }
                catch (FifaLogicException ex)
                {
                    _logger.LogWarning("Main", "Logic Exception: " + ex.Message);
                    errorTrigger++;

                    if (errorTrigger >= 3)
                    {
                        break;
                    }
                }
                catch (FifaInternalServerException e)
                {
                    _logger.LogWarning("Main", "Issue with server task sleep for 10 minutes");
                    await Task.Delay(10 * 60 * 1000);
                }
                catch (FifaSessionExceptionCaptcha e)
                {
                    _logger.Log("Main", "Session Captcha: " + e.Message);
                    //Must call it when trigger funcaptcha, then you need to login again and solve it, method setup IsLogged false to help you, at this point you can also force people to solve captcha by their self if dont wanna use solver after logout
                    //Remember must be solved only when trigger on GetUserInfo() not here
                    await futClient.LogOut();
                }
                catch (FifaSessionException e)
                {
                    _logger.LogWarning("Main", "Session: " + e.Message);

                    if (errorTrigger >= 3)
                    {
                        break;
                    }

                    //Usually happen when somebody is logged at same time
                    await futClient.ReSession();
                }
                catch (FifaException e)
                {
                    _logger.LogError("Main", "FifaException: " + e.Message);
                    errorTrigger++;

                    if (errorTrigger >= 3)
                    {
                        break;
                    }
                }
                catch (TaskCanceledException)
                {
                    //Network Issue? Perfomance Issue?
                    await Task.Delay(10 * 1000);
                }
                catch (Exception e)
                {
                    _logger.LogError("Main", "Exception: " + e.Message);
                    errorTrigger++;

                    if (errorTrigger >= 3)
                    {
                        break;
                    }
                }

                int secPause = FutManager.RandomNumber(2 * 10, 4 * 10);
                await Task.Delay(secPause * 100);
            }

            futClient.Dispose(); //Remember to Dispose when finish to use instance to release resources
        }

        private static async Task WorksProcess(IFutClient futClient)
        {
            if (!await IsLogged(futClient))
                return;

            //Now You are Logged then can perform other calls

            //At this point you should be your method to check Watchlist Status and Manage it ...
            if (NextWatchListCheck < DateTime.UtcNow)
            {
                NextWatchListCheck = DateTime.UtcNow.AddMinutes(5);
                var watchlistData = await futClient.GetWatchList();
                WatchList = watchlistData.AuctionInfo;
                Coins = watchlistData.Credits;
                return;
            }

            //WatchList Worker
            // rember to apply change on your Watchlist when call methods (example if you call RemoveOutBidFromWatchlist rember to delete on list removed items) 

            //futClient.RemoveOutBidFromWatchlist
            //futClient.ListItem < First to call It from Watchlist rember to call PriceLimitsByItemId to get price range then rember to add on your List TradePile new item (cause you call update if not expire 5 minutes)(need to cast to FifaAuction), try to Catch FifaStuckException for trade stuck
            //futClient.SendItemToTradePile < rember to add to TradePile new item, try to Catch F
            //futClient.PlaceBid < rember to put inside try catch and intercept FifaPermissionDeniedException when somebody is more fast than your or FifaStuckException to detect bid stuck

            //when you are in watchlist "cycle" cause you bidding rember to update auction status with GetAuctionsStatusInfo(...)
            //Usually you need to create a list of items to update by default EA will call each sec if expire in less 30 secs, each 5 sec if expire under 60 sec, each 2 minutes if expire under 10 minutes, other each 10 minutes


            //At this point you should be your method to check Tradepile Status and Manage it ...
            if (NextTradePileCheck < DateTime.UtcNow)
            {
                NextTradePileCheck = DateTime.UtcNow.AddMinutes(5);
                var tradeData = await futClient.GetTradePile();
                TradePile = tradeData.AuctionInfo;
                Coins = tradeData.Credits;
                return;
            }

            //TradePile Worker

            //futClient.RemoveSoldFromTradePile < then must call GetTradePile()
            //futClient.RelistTradePile < catch FifaPermissionDeniedException when happen you need to list one by one, then must call GetTradePile()
            //futClient.ListItem < catch FifaStuckException for stuck, then must call GetTradePile()


        }

        private static async Task<bool> IsLogged(IFutClient futClient)
        {
            if (futClient.IsLogged)
                return true;

            string fifaEmail = "fut@acc.com";
            string fifaPass = "fifaPass";
            string proxyAddress = null; //"192.168.1.2:8888";
            string proxyUser = null;
            string proxyPass = null;
            Regex rgx = new Regex("[^a-zA-Z0-9]");
            string storageFile = Path.Combine(AssemblyPath, rgx.Replace(fifaEmail, "") + ".storage.bin"); //must be unique for each account
            string solverApiKey = ""; //Get key on http://api.de-ltd.co.uk/login
            bool useFunSolverProxy = false; //if true require proxy setup on loggin function
            ITwoFactorCodeProvider twoFactorCodeProvider = new AppAuthTwoFactorCodeProvider("xxxxxxxxxxxxx"); //You can also use ImapAuthTwoFactorCodeProvider, DisabledTwoFactorCodeProvider, AppAuthTwoFactorCodeProvider or implement a new by your self

            //Will throw Exception if detect new fifa code, then not need to use FutManager.IsFifaSupported before call it
            var accountType = await futClient.Loggin(fifaEmail, fifaPass, twoFactorCodeProvider, proxyAddress, proxyUser, proxyPass, storageFile);
            _logger.Log("IsLogged", "Logged on accountType " + accountType);

            FifaUserResponse accountInfo = null;
            try
            {
                accountInfo = await futClient.GetUserInfo();
            }
            catch (FifaSessionExceptionCaptcha)
            {
                //Need to solve captcha before we can continue

                //We use futmsolver solver but you can add a new one just implement here ... or you can also stop bot and ask to people to solve in standard app

                if (useFunSolverProxy)
                {
                    //Require proxy setup on Loggin function or will return error
                    if (!await futClient.FuncaptchaProxySolver(solverApiKey))
                        throw new Exception("too many tenatives");
                }
                else
                {
                    //We need to request captcha data before call funcaptcha
                    var captchaData = await futClient.GetCaptchaKey();
                    string callBack = null;

                    //It try to make max 3 tentatives
                    for (byte t = 0; t < 3; t++)
                    {
                        callBack = await futClient.FuncaptchaSolver(solverApiKey, captchaData.Pk, captchaData.Blob);
                        if (!string.IsNullOrEmpty(callBack))
                        {
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(callBack))
                        throw new Exception("too many tenatives");

                    //need to validate code on EA side
                    bool result = await futClient.ValidateCaptchaKey(callBack);
                    if (!result)
                        throw new Exception("usually never happen, session expired ?!?");
                }

                //Call again after captcha solved
                accountInfo = await futClient.GetUserInfo();
            }

            //0: "NONE" 1: "BLACKLIST" 2: "WHITELIST" 3: "GREYLIST" 100: "MAINTENANCE"
            if (accountInfo.UserInfo.Feature.Trade == 0)
            {
                throw new FifaConfigException("MarketNotUnlocked");
            }
            else if (accountInfo.UserInfo.Feature.Trade == 1)
            {
                throw new FifaConfigException("MarketLockedBlack");
            }
            else if (accountInfo.UserInfo.Feature.Trade == 3)
            {
                throw new FifaConfigException("MarketLockedGrey");
            }

            Coins = accountInfo.UserInfo.Credits; //Usually you will update credits when call GetTrade/GetWatch

            //Must Use it cause EA cache data for 5 minutes then you can't ask Each time this data
            NextTradePileCheck = DateTime.UtcNow.AddMinutes(5);
            NextWatchListCheck = DateTime.UtcNow.AddMinutes(5);
            NextUnassignedCheck = DateTime.UtcNow.AddMinutes(5);

            var liveMessages = await futClient.GetLiveMessage(); //Call only here no other part

            var unassignedData = await futClient.GetUnassignedItems();
            var tradeData = await futClient.GetTradePile(); //You can use Credits inside to update current credit when call it
            var watchlistData = await futClient.GetWatchList(); //You can use Credits inside to update current credit when call it

            var activeMessages = await futClient.GetActiveMessage(); //Call only here no other part

            Unassigned = unassignedData.ItemData; //DuplicateItemIdList should be use to know if you can send player to club
            TradePile = tradeData.AuctionInfo;
            WatchList = watchlistData.AuctionInfo;

            foreach (var message in liveMessages?.MessageList)
            {
                _logger.Log("IsLogged", "LiveMessage " + message.Renders.FirstOrDefault(i => i.Name == "bodyText").Value);
                await Task.Delay(FutManager.RandomNumber(30, 50) * 100);
                await futClient.CloseLiveMessage(message.MessageId);
            }

            foreach (var message in activeMessages?.ActiveMessage)
            {
                _logger.Log("IsLogged", "ActiveMessage You have Active Message message[" + message.Message + "] id[" + message.Id + "] rewardType[" + message.RewardType + "] rewardValue[" + message.RewardValue + "]");
                await Task.Delay(FutManager.RandomNumber(30, 50) * 100);
                await futClient.CloseActiveMessage(message.Id);
            }

            _logger.Log("IsLogged", "Logged");
            return true;
        }

        private async Task<List<FifaAuctionRestricted>> SearchInMarket(IFutClient futClient, FifaInvBase item, short page, uint maxBuynow = 0, uint maxBid = 0, uint minBid = 0, uint minBuyNow = 0)
        {
            //usually need to increase each page and not jump from 0 to 50 for example, but you can do it at your own risk :D
            //Rember you can BID/BIN only items on Page that you current request, you can not for example store 3 pages than try to bid result on first page or will return error
            //when you are in Search Page cause you bidding/bin rember to update auctions status with GetAuctionsStatusInfo(...)
            //Usually you need to create a list of items to update by default EA will call each sec if expire in less 30 secs, each 5 sec if expire under 60 sec, each 2 minutes if expire under 10 minutes, other each 10 minutes
            //Rember EA cached results for 30 secs with same parameters then if you example try to bin sniping and refreshing page you need to change almost 1 parameter to avoid cached result like increase/decrease price

            if (page < 1)
                throw new ArgumentException("Start Page under 1");

            FifaSearchParameters _searchargs;

            switch (item.ItemType)
            {
                case FifaMarketType.Player:

                    _searchargs = new FifaPlayerSearchParameters()
                    {
                        //ChemistryStyle = targetType,
                        //PreferredPosition = targetType,
                    };

                    FifaInvPlayer fifaInvPlayer = (FifaInvPlayer)item;

                    //(EnableUnsafeCode)
                    //_searchargs.ResourceId = fifaInvPlayer.Id; //On Web app usually you are not allow to do it
                    //else
                    {
                        _searchargs.ResourceId = fifaInvPlayer.AssetId;
                        ((FifaPlayerSearchParameters)_searchargs).CardType = fifaInvPlayer.CardType != CardType.Special ? CardType.Any : fifaInvPlayer.CardType;
                    }

                    break;

                case FifaMarketType.PlayersCustom:
                    _searchargs = new FifaPlayerSearchParameters()
                    {
                        /*
                        CardType = itemPsearch.CardType,
                        ChemistryStyle = itemPsearch.ChemistryStyle,
                        League = itemPsearch.LeagueId,
                        Nation = itemPsearch.NationId,
                        Team = itemPsearch.TeamId,
                        PreferredPosition = itemPsearch.PreferredPosition,
                        */
                    };

                    break;

                case FifaMarketType.Contract:
                    _searchargs = new FifaDevelopmentSearchParameters(DevelopmentType.contract);

                    //if (EnableUnsafeCode)
                    //((FifaDevelopmentSearchParameters)_searchargs).DefinitionId = item.Id; //On Web app usually you are not allow to do it

                    break;

                case FifaMarketType.FitnessCard:
                    _searchargs = new FifaDevelopmentSearchParameters(DevelopmentType.fitness);

                    //if (EnableUnsafeCode)
                    //((FifaDevelopmentSearchParameters)_searchargs).DefinitionId = item.Id; //On Web app usually you are not allow to do it

                    break;

                case FifaMarketType.HealtCard:
                    _searchargs = new FifaDevelopmentSearchParameters(DevelopmentType.healing);

                    //if (EnableUnsafeCode)
                    //((FifaDevelopmentSearchParameters)_searchargs).DefinitionId = item.Id; //On Web app usually you are not allow to do it

                    break;

                case FifaMarketType.PlayStyle:
                    _searchargs = new FifaTrainingSearchParameters(TrainingType.playStyle)
                    {
                        ChemistryStyleType = (ChemistryStyleType)item.Id,
                    };

                    break;

                case FifaMarketType.Position:
                    _searchargs = new FifaTrainingSearchParameters(TrainingType.position)
                    {
                        PositionChangeType = (PositionChangeType)item.Id,
                    };

                    break;

                case FifaMarketType.PlayerTraining:
                    _searchargs = new FifaTrainingSearchParameters(TrainingType.playerTraining);

                    //if (EnableUnsafeCode)
                    //((FifaDevelopmentSearchParameters)_searchargs).DefinitionId = item.Id; //On Web app usually you are not allow to do it


                    break;

                default:
                    throw new Exception("MarketWorker unknown ItemMarket Type");
            }

            _searchargs.MaxBid = maxBid;
            _searchargs.MaxBuy = maxBuynow;
            _searchargs.MinBid = minBid;
            _searchargs.MinBuy = minBuyNow;
            _searchargs.Page = page;

            try
            {
                var callresponse = await futClient.Search(_searchargs);
                return new List<FifaAuctionRestricted>(callresponse.AuctionInfo);
            }
            catch (FifaStuckException e)
            {
                //try to catch 3 times in row before flag stuck
                return null;
            }
        }

        //Usually dev should be call and provide this DB cause can be a risk make all this calls, or you can create other type of method like call specific player maybe import from futbin?
        //This method will update also clubId cause on GenInvPlayers is not possible know it
        private static async Task GenInform(IFutClient futClient)
        {
            var targetlist = FutManager.InvDb.Where(i => (i.Value is FifaInvPlayer) && (i.Value.CardType == CardType.Gold || i.Value.CardType == CardType.Silver)).Select(i => (FifaInvPlayer)i.Value).ToList();
            var total = targetlist.Count();
            for (int i = 0; i < total; i++)
            {
                _logger.Log("GenInform", "process " + i + "/" + total);
                try
                {
                    await futClient.RequestPlayerVariants(targetlist[i].AssetId);
                }
                catch (FifaSessionException e)
                {
                    //when so much call trigger it
                    await futClient.ReSession();

                    await futClient.RequestPlayerVariants(targetlist[i].AssetId);
                }
            }

            //FutManager.InvDb is updated by it self then just save it
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, FutManager.InvDb);
            await File.WriteAllBytesAsync("invDb.proto", stream.ToArray());
        }

        private static async Task LoadGenInvDb()
        {
            //We use ProtoBuf protocol to store data
            string fileName = "invDb.proto";

            if (File.Exists(fileName))
            {
                byte[] data = await File.ReadAllBytesAsync(fileName);
                using var streamFile = new MemoryStream(data);
                //Load All data inside Library, cause this data will be use also for internal call like checking player name/info, you can use same data to check item/player in your code
                FutManager.InvDb = Serializer.Deserialize<Dictionary<uint, FifaInvBase>>(streamFile);
                return;
            }

            //Load All data inside Library, cause this data will be use also for internal call like checking player name/info, you can use same data to check item/player type in your code
            FutManager.InvDb = new Dictionary<uint, FifaInvBase>();

            //Add Inv Items to our Db, usually dont need to create/update it cause data are static and not update during season contracts/fitness/healt ...
            await FutManager.GenInvItems(FutManager.InvDb);
            //Icons and Standard player (no inform) dont need to update so offen, cause inform players are not here, to add Inform must use inside a instance seassion search Variants or try to add by other method like try to import from futbin ?
            await FutManager.GenInvPlayers(FutManager.InvDb);

            using var stream = new MemoryStream();
            Serializer.Serialize(stream, FutManager.InvDb);
            await File.WriteAllBytesAsync(fileName, stream.ToArray());
        }
    }
}
