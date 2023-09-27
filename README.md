Code to utilize the RavenDB C# client and subscription services to create a system to handle a marketplace for buying and selling items defined in https://github.com/TylerLig/StashExample.

# Program Description
RetrieveNewOffers:
Subscribes to new user offers.
For each offer received, flags it as aggregated (Aggregated = true) and sends a patch request the UserData database.

AggregateNewOffers(BaseExchangeOffer offerData):
Adds the incoming offer to a dictionary holding batched exchange offer data called BatchData.

AggregateBatchTimer:
Periodically send new offer information contained in BatchData to the Exchange Database.

MatchOffers():
Subscribes to new aggregate offers.
Will match offers between buyers and sellers and will remove matched offers from the aggregate and update the respective owner of the offer with the new status.

## Overall Flow
![archDiagramLabeled2](https://github.com/TylerLig/RavenDbClientExample/assets/29814578/c3ddd541-f494-4244-9cba-ca156bdc3f94)

UserData is a data store that will contain information about user specific offers. 
An example of what a document for a particular user might look like, this user has two sell offers for two different items of the same type:

![userdata](https://github.com/TylerLig/RavenDbClientExample/assets/29814578/9c3c27c3-6a77-4d25-810d-bef6eb42668f)

Exchange is a data store that will contain information about all offers grouped by item type. An example of what a document might for an item type 'ID_Test6' might look like, this document contains the two sell offers that are owned by the same user depicted in the UserData example as well as a buy offer owned by another user.

![exchange](https://github.com/TylerLig/RavenDbClientExample/assets/29814578/77adc88c-179d-4c32-b5fa-ad45ee0b5dbd)

Using the data depicted as an example, the first buy offer and first sell offer would be matched as there is a buyer offering to buy 2 items for a price of 9 and a sell offer that is selling that item for a price of 8 fulfills that requirement. 
This would result in the buy offer losing 1 count, but remaining in the Exchange, and the sell offer being completely removed.
The UserData for both the buyer and seller would be updated to reflect the updated status, allowing the currency to be obtained by the seller and the item to the buyer.
