using Raven.Client.Documents;
using Raven.Client.Documents.Commands.Batches;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Subscriptions;
using System.Configuration;
using Raven.Client.Documents.Session;

string DatabaseHost = ConfigurationManager.AppSettings.Get("DatabaseHost");
string UserDataDatabaseName = ConfigurationManager.AppSettings.Get("UserDataDatabaseName");
string ExchangeDatabaseName = ConfigurationManager.AppSettings.Get("ExchangeDatabaseName");
string RetrieveNewUserOffersSubscriptionName = ConfigurationManager.AppSettings.Get("RetrieveNewUserOffersSubscriptionName");
string MatchOffersSubscriptionName = ConfigurationManager.AppSettings.Get("MatchOffersSubscriptionName");
TimeSpan AggregateBatchInterval = TimeSpan.FromSeconds(1);
TimeSpan.TryParse(ConfigurationManager.AppSettings.Get("AggregateBatchInterval"), out AggregateBatchInterval);

IDocumentStore ExchangeStore = new DocumentStore
{
    Urls = new[] { DatabaseHost },
    Database = ExchangeDatabaseName
}.Initialize();

IDocumentStore UserDataStore = new DocumentStore
{
    Urls = new[] { DatabaseHost },
    Database = UserDataDatabaseName
}.Initialize();

//Maps (Document ID -> New Buy/Sell Offers) to be sent
Dictionary<string, ExchangeOfferData> BatchData = new Dictionary<string, ExchangeOfferData>();
object BatchDataLock = new();

//The BatchData dictionary will be populated with new offers provided by AggregateNewOffers method,
//Periodically, AggregateBatchTimer will run and send this new offer information to the Exchange Database
Timer AggregateBatchTimer = new Timer(_ =>
{
    Console.WriteLine($"AggregateBatchTimer {DateTime.UtcNow}");
    if (BatchData.Count < 1)
        return;
    Console.WriteLine($"AggregateBatch {BatchData.Count} items");
    lock (BatchDataLock)
    {
        using IDocumentSession session = ExchangeStore.OpenSession();
        foreach ((string docId, ExchangeOfferData exchangeOfferDataToAdd) in BatchData)
        {
            ExchangeOfferData existingExchangeOfferData = session.Load<ExchangeOfferData>(docId) ?? new ExchangeOfferData();
            existingExchangeOfferData.SellOffers.AddRange(exchangeOfferDataToAdd.SellOffers);
            existingExchangeOfferData.BuyOffers.AddRange(exchangeOfferDataToAdd.BuyOffers);
            session.Store(existingExchangeOfferData, docId);
        }
        session.SaveChanges();
        BatchData.Clear();
    }
}, null, AggregateBatchInterval, AggregateBatchInterval);

await Task.WhenAll(RetrieveNewOffers(), MatchOffers());


async Task RetrieveNewOffers()
{
    SubscriptionWorker<UserOffers> retrieveNewUserOffersSubscriptionWorker = UserDataStore.Subscriptions.GetSubscriptionWorker<UserOffers>(
        new SubscriptionWorkerOptions(RetrieveNewUserOffersSubscriptionName)
        {
            //optionals
        }
    );

    Task retrieveNewUserOffersSubscriptionTask = retrieveNewUserOffersSubscriptionWorker.Run(batch =>
    {
        Console.WriteLine($"Recieved new offers from subscription {DateTime.UtcNow}");
        foreach (SubscriptionBatch<UserOffers>.Item item in batch.Items)
        {
            string[] splitString = item.Id.Split('/'); //Assuming document id follows the pattern {UserId}/Offers
            if (splitString.Length != 2)
                continue;

            string userId = splitString[0];

            UserOffers offerDocument = item.Result;
            using IDocumentSession session = UserDataStore.OpenSession();
            foreach (SellOfferData sellOffer in offerDocument.SellOffersData)
            {
                if (sellOffer.Aggregated)
                    continue;

                AggregateNewOffers(new ExchangeSellOfferData()
                {
                    OfferId = sellOffer.OfferId,
                    UserId = userId,
                    Price = sellOffer.Price,
                    ItemInstance = sellOffer.ItemInstance,
                });

                PatchRequest patchRequest = new PatchRequest
                {
                    Script = @"this.SellOffersData.find(sellOffer => sellOffer.OfferId === args.offerId).Aggregated = args.aggregatedValue;",
                    Values =
                        {
                            { "offerId", sellOffer.OfferId },
                            { "aggregatedValue", true }
                        }
                };
                session.Advanced.Defer(new PatchCommandData(item.Id, null, patchRequest, null));
            }
            foreach (BuyOfferData buyOffer in offerDocument.BuyOffersData)
            {
                if (buyOffer.Aggregated)
                    continue;

                AggregateNewOffers(new ExchangeBuyOfferData()
                {
                    OfferId = buyOffer.OfferId,
                    UserId = userId,
                    Price = buyOffer.Price,
                    ItemDefinition = buyOffer.ItemDefinition,
                });

                PatchRequest patchRequest = new PatchRequest
                {
                    Script = @"this.BuyOffersData.find(buyOffer => buyOffer.OfferId === args.offerId).Aggregated = args.aggregatedValue;",
                    Values =
                        {
                            { "offerId", buyOffer.OfferId },
                            { "aggregatedValue", true }
                        }
                };
                session.Advanced.Defer(new PatchCommandData(item.Id, null, patchRequest, null));
            }
            session.SaveChanges();
        }
    });

    await retrieveNewUserOffersSubscriptionTask;
}

void AggregateNewOffers(BaseExchangeOffer offerData)
{
    string docId;
    lock (BatchDataLock)
    {
        switch (offerData)
        {
            case ExchangeSellOfferData sellOffer:
                docId = sellOffer.ItemInstance.ItemDefinition;
                if (!BatchData.ContainsKey(docId))
                {
                    ExchangeOfferData exchangeOfferData = new ExchangeOfferData();
                    BatchData[docId] = exchangeOfferData;
                }
                BatchData[docId].SellOffers.Add(sellOffer);
                break;

            case ExchangeBuyOfferData buyOffer:
                docId = buyOffer.ItemDefinition;
                if (!BatchData.ContainsKey(docId))
                {
                    ExchangeOfferData exchangeOfferData = new ExchangeOfferData();
                    BatchData[docId] = exchangeOfferData;
                }
                BatchData[docId].BuyOffers.Add(buyOffer);
                break;
        }
    }
}

async Task MatchOffers()
{
    SubscriptionWorker<ExchangeOfferData> matchOffersSubscriptionWorker = ExchangeStore.Subscriptions.GetSubscriptionWorker<ExchangeOfferData>(
        new SubscriptionWorkerOptions(MatchOffersSubscriptionName)
        {
            //optionals
        }
    );

    Task matchOffersSubscriptionTask = matchOffersSubscriptionWorker.Run(batch =>
    {
        Console.WriteLine($"Recieved request to Match Offers {DateTime.UtcNow}");
        foreach (SubscriptionBatch<ExchangeOfferData>.Item item in batch.Items)
        {
            
        }
    });

    await matchOffersSubscriptionTask;
}