using UnityEngine;

// RevenueCat pushes customer info here whenever it changes - including a
// purchase that completed outside the app (an App Store promo, a purchase that
// finished while the app was backgrounded, a subscription of someone else's
// making). Routing it through the same grant path as everything else is what
// makes those cases pay out at all.
public class IapListener : Purchases.UpdatedCustomerInfoListener
{
  public override void CustomerInfoReceived(Purchases.CustomerInfo customerInfo)
  {
    IapGrant.ProcessCustomerInfo(customerInfo);
  }
}
