public class UserOffers
{
    public BuyOfferData[] BuyOffersData { get; set; }
    public SellOfferData[] SellOffersData { get; set; }
}

public class SellOfferData
{
    public string OfferId { get; set; }
    public bool Aggregated { get; set; }
    public int Price { get; set; }
    public ItemInstance ItemInstance { get; set; }
}

public class BuyOfferData
{
    public string OfferId { get; set; }
    public bool Aggregated { get; set; }
    public int Price { get; set; }
    public string ItemDefinition { get; set; }
}

public class BaseExchangeOffer
{
}

public class ExchangeSellOfferData : BaseExchangeOffer
{
    public string OfferId { get; set; }
    public string UserId { get; set; }
    public int Price { get; set; }
    public ItemInstance ItemInstance { get; set; }
}

public class ExchangeBuyOfferData : BaseExchangeOffer
{
    public string OfferId { get; set; }
    public string UserId { get; set; }
    public int Price { get; set; }
    public string ItemDefinition { get; set; }
}

public class ItemInstance
{
    public string ItemDefinition { get; set; }
    public List<Fragment> Fragments { get; set; }
}

public class Fragment
{
    public string FragmentType { get; set; }
    public Dictionary<string, object> Data { get; set; }
}

public class ExchangeOfferData
{
    public List<ExchangeBuyOfferData> BuyOffers { get; set; } = new();
    public List<ExchangeSellOfferData> SellOffers { get; set; } = new();
}
