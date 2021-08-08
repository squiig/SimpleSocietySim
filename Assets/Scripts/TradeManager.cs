using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TradeManager : MonoBehaviour
{
	private GameManager _gameManager;

	private void Awake()
	{
		_gameManager = FindObjectOfType<GameManager>();
	}

	public bool TryMakeBoxTrade(Citizen buyer, Citizen seller, int initialUnitAmount, float initialUnitPrice)
	{
		float finalTotalPrice;
		int finalAmount;
		bool isDealMade = TryNegotiateTradeDeal(initialUnitAmount, initialUnitPrice, buyer, seller, out finalAmount, out finalTotalPrice);

		if (isDealMade)
		{
			MakeResBoxTrade(buyer, seller, finalAmount, finalTotalPrice);
			return true;
		}

		Debug.Log("Trade <color=black>canceled</color>.");
		return false;
	}

	void MakeResBoxTrade(Citizen buyer, Citizen seller, int amount, float totalPrice)
	{
		if (amount > seller.TotalResBoxesOwned)
		{
			Debug.LogWarning($"Warning: {buyer.name} (b) and {seller.name} (s) tried to trade {amount} {GameManager.ResBoxSymbol}, but seller only has {seller.TotalResBoxesOwned} {GameManager.ResBoxSymbol}!");
			return;
		}

		// Bookkeeping
		seller.RegisterSale(totalPrice, amount);
		_gameManager.RegisterSale(totalPrice, amount);

		// Transaction
		seller.AddMoney(totalPrice);
		buyer.AddMoney(-totalPrice);
		seller.AddResBoxes(-amount);
		buyer.AddResBoxes(amount);

		Debug.Log($"<color=yellow><b>{seller.name}</b> sold <b>{amount} {GameManager.ResBoxSymbol}</b> to <b>{buyer.name}</b> for <b>{GameManager.FormatMoney(totalPrice)}</b>!</color>");
	}

	bool TryNegotiateTradeDeal(in int unitAmount, in float unitOffer, in Citizen buyer, in Citizen seller, out int finalAmount, out float finalTotalPrice, string logString = "", bool isFinal = false)
	{
		finalTotalPrice = float.PositiveInfinity;
		finalAmount = unitAmount;

		if (unitAmount <= 0)
		{
			Debug.LogWarning($"Warning: {buyer.name} (b) and {seller.name} (s) tried to trade {unitAmount} {GameManager.ResBoxSymbol}!");
			return false;
		}

		/*
		 * Can the seller even handle this order?
		 */
		if (unitAmount > seller.TotalResBoxesOwned)
		{
			logString += $"{seller.name} can't sell {unitAmount} {GameManager.ResBoxSymbol} to {buyer.name} because the order is <b>too big</b>.\n";

			if (seller.TotalResBoxesOwned > 1)
			{
				logString += "Let's try half the amount.\n";
				int newAmount = Mathf.RoundToInt(unitAmount / 2);
				return TryNegotiateTradeDeal(newAmount, unitOffer, buyer, seller, out finalAmount, out finalTotalPrice, logString);
			}

			if (seller.TotalResBoxesOwned == 1) // Does the seller have a box at all?
			{
				logString += "Let's try just ONE box.\n";
				return TryNegotiateTradeDeal(1, unitOffer, buyer, seller, out finalAmount, out finalTotalPrice, logString, true);
			}

			Debug.Log(logString);
			return false;
		}

		/*
		 * A first price is drafted that favors the seller, if possible. (temporarily not the case)
		 */
		float fairestUnitPrice = seller.MinUnitSellingPrice + ((unitOffer - seller.MinUnitSellingPrice) / 2);
		//float initialUnitPrice = unitOffer > fairestUnitPrice ? unitOffer : fairestUnitPrice;
		float buyerMaxTotalPrice = unitAmount * buyer.MaxUnitBuyingPrice;
		float sellerMinimumTotalPrice = unitAmount * seller.MinUnitSellingPrice;
		finalTotalPrice = unitAmount * (isFinal ? unitOffer : fairestUnitPrice);

		if (isFinal)
			logString += $"The final offer is <b>{finalAmount} {GameManager.ResBoxSymbol} for {GameManager.FormatMoney(finalTotalPrice)}</b>...\n";
		else
			logString += $"The negotation starts at <b>{finalAmount} {GameManager.ResBoxSymbol} for {GameManager.FormatMoney(finalTotalPrice)}</b>...\n";

		/*
		 * If the customer can't afford that, the order amount could be lowered or the seller could try to make a final, minimum offer.
		 */
		if (!buyer.CanPay(finalTotalPrice))
		{
			logString += $"{buyer.name} <b>can't afford</b> {GameManager.FormatMoney(finalTotalPrice)}...\n";

			/*
			 * First, let's try lowering the amount.
			 */
			if (unitAmount > 1)
			{
				logString += "Let's try half the amount.\n";
				int newAmount = Mathf.RoundToInt(unitAmount / 2);
				return TryNegotiateTradeDeal(newAmount, unitOffer, buyer, seller, out finalAmount, out finalTotalPrice, logString);
			}

			/*
			 * If that doesn't cut it, the seller will make his final offer for 1 box.
			 */
			float finalOffer = sellerMinimumTotalPrice;

			logString += $"{seller.name} offers a <b>final deal</b> of {finalAmount} {GameManager.ResBoxSymbol} to {buyer.name} for <b>{GameManager.FormatMoney(finalOffer)}</b>...\n";

			/*
			 * If the customer can afford the final offer, that will be the deal.
			 */
			if (!buyer.CanPay(finalOffer))
			{
				logString += $"{buyer.name} still <b>can't afford</b> it.\n";

				Debug.Log(logString);
				return false; // Otherwise the order will have to be canceled altogether
			}

			finalTotalPrice = finalOffer;
		}

		/*
		 * After it's determined if the buyer can pay, both will decide if it's worth it to them.
		 */
		if (finalTotalPrice > buyerMaxTotalPrice)
		{
			logString += $"{buyer.name} <b>won't buy</b> {finalAmount} {GameManager.ResBoxSymbol} from {seller.name} for {GameManager.FormatMoney(finalTotalPrice)} because it's <b>not worth it.</b>\n";

			if (isFinal)
			{
				Debug.Log(logString);
				return false;
			}
			else return TryNegotiateTradeDeal(unitAmount, buyer.MaxUnitBuyingPrice, buyer, seller, out finalAmount, out finalTotalPrice, logString, Random.value > .5);
		}

		if (finalTotalPrice < sellerMinimumTotalPrice)
		{
			logString += $"{seller.name} <b>won't sell</b> {finalAmount} {GameManager.ResBoxSymbol} to {buyer.name} for {GameManager.FormatMoney(finalTotalPrice)} because it's <b>not worth it.</b>\n";

			if (isFinal)
			{
				Debug.Log(logString);
				return false;
			}
			else return TryNegotiateTradeDeal(unitAmount, seller.MinUnitSellingPrice, buyer, seller, out finalAmount, out finalTotalPrice, logString, Random.value > .5);
		}

		Debug.Log(logString);
		return true;
	}
}
