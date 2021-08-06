using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class Citizen : MonoBehaviour
{
	public enum BusinessStrategy
	{
		NONE,
		GATHERING,
		BUYING,
		SELLING,
		SERVICING
	}

	GameManager _gameManager;
	Vector3 _origin;
	Vector3 _target;
	GameObject _targetObject;
	Vector3 _anchor;
	float _timer;
	float _totalSeconds;
	float _restrategizingMoment;

	[Header("Boring Stuff")]
	public TMP_Text moneyLabel;
	public TMP_Text secondLabel;
	public TMP_Text thirdLabel;

	[SerializeField] float _speed = 5f;
	[SerializeField] float _fieldRadius = 20f;
	[SerializeField] float _idlingRadius = .5f;

	public bool IsFrozen;

	[Header("Interesting Stuff")]
	[Tooltip("Starts out random between 0 and 1, but can fluctuate by effect of profit margins.")]
	public float baseResBoxValuation;

	[Tooltip("The primary asset that can be found or traded as merchandise.")]
	public int totalResBoxesOwned;

	[Tooltip("The amount of seconds to wait before deciding on the next action.")]
	[SerializeField] float _opportunityWaitingTime = 2f;

	[Tooltip("The amount of seconds to wait before trying to trade again with the last trade partner that failed to strike a deal.")]
	[SerializeField] float _badTradePartnerBanTime = 10f;

	[Tooltip("The citizen won't accept a profit margin lower than this decimal fraction.")]
	[SerializeField] float _minimumProfitExpectation = 0.01f;

	[Tooltip("The profit margin (in decimal fraction) the citizen will open with at its first trading negotiation.")]
	[SerializeField] float _startingProfitExpectation = 0.1f;

	[Tooltip("The decimal fraction of this citizen's total worth ")]
	[SerializeField] float _tradeInvestmentFraction = 0.2f;

	[Header("Read-Only")]
	[SerializeField, ReadOnly] float _moneyInWallet;
	[SerializeField, ReadOnly] float _startingCapital;
	[SerializeField, ReadOnly] float _maxBuyingPriceMetric;
	[SerializeField, ReadOnly] float _minSellingPriceMetric;
	//[SerializeField, ReadOnly] bool _isDoomed; // meaning they dont have an existing cashflow + dont own assets + cant afford any type of investment right now
	[SerializeField, ReadOnly] bool _isIdling; // just for aesthetic effect, doesn't cost money
	[SerializeField, ReadOnly] BusinessStrategy _currentStrategy;
	//[SerializeField, ReadOnly] bool _wantsToTrade;
	[SerializeField, ReadOnly] int _totalResBoxesGathered;
	[SerializeField, ReadOnly] float _totalGatheringTravelCosts;
	[SerializeField, ReadOnly] float _latestProfit;
	[SerializeField, ReadOnly] float _latestProfitMargin;
	[SerializeField, ReadOnly] float _profitGrowth;
	[SerializeField, ReadOnly] float _currentProfitExpectation;
	[SerializeField, ReadOnly] float _currentBuyingGoalMetric;
	[SerializeField, ReadOnly] float _currentSellingGoalMetric;
	[SerializeField, ReadOnly] Citizen _lastFailedTradePartner;

	public float MoneyInWallet => _moneyInWallet;

	public float AverageResBoxAcquisitionCost => _totalGatheringTravelCosts / _totalResBoxesGathered;

	/// <summary>
	/// Yields no profit, never goes below the citizen's base valuation of one res box.
	/// </summary>
	public float MinimumSellingPrice => AverageResBoxAcquisitionCost + Mathf.Max(baseResBoxValuation, baseResBoxValuation / (totalResBoxesOwned + 1) * _gameManager.priceMagnifier);

	public float MaxBuyingPrice => MinimumSellingPrice * (1 - _minimumProfitExpectation); // wants at the very least the profit potential he currently expects

	public float CurrentBuyingPriceGoal => MinimumSellingPrice * (1 - _currentProfitExpectation);

	public float CurrentSellingPriceGoal => MinimumSellingPrice * (1 + _currentProfitExpectation);

	public float ProfitGrowth => _profitGrowth;

	public bool AreProfitsIncreasing => ProfitGrowth > 0;

	public float TotalProfits => MoneyInWallet - _startingCapital;

	public float ProfitPerSecond => TotalProfits / _totalSeconds;

	public BusinessStrategy CurrentStrategy => _currentStrategy;

	public event Action DestinationReached;

	public void AddMoney(float money)
	{
		_moneyInWallet += money;
	}

	public void GiveStartingCapital(float amount)
	{
		AddMoney(amount);
		_startingCapital = amount;
	}

	public bool TryBuyBox(int startingAmount, float unitOffer, Citizen buyer)
	{
		// Considerations
		float finalTotalPrice;
		int finalAmount;
		bool isDealSuccessful = TryNegotiateTradeDeal(startingAmount, out finalAmount, unitOffer, buyer, out finalTotalPrice);

		if (!isDealSuccessful)
		{
			Debug.Log("Trade <color=black>canceled</color>.");
			return false;
		}

		// Perform the sale
		SellResBox(finalAmount, finalTotalPrice, buyer);

		return true;
	}

	public bool TrySellBox(int amount, float unitOffer, Citizen seller)
	{
		Debug.Log($"<color=red><b>{seller.name}</b> wants to <b>sell {amount} {GameManager.ResBoxSymbol}</b> to <b>{name}</b> for <b>{GameManager.FormatMoney(unitOffer * amount)}</b>...</color>");

		return seller.TryBuyBox(amount, unitOffer, this);
	}

	public bool CanPay(float price)
	{
		return price <= MoneyInWallet;
	}

	public bool TryAskForAttention()
	{
		IsFrozen = true;
		_currentStrategy = BusinessStrategy.NONE;
		return true; // always succeeds for now
	}

	public void ReleaseAttention()
	{
		IsFrozen = false;
		SetIdling(true);
	}

	public void Halt(float timeUntilRestrategizing = 0f)
	{
		_anchor = transform.position;
		_target = _anchor;
		_targetObject = null;
		DestinationReached = null;
		_restrategizingMoment = _totalSeconds + timeUntilRestrategizing;
	}

	private void Awake()
	{
		_gameManager = FindObjectOfType<GameManager>();

		_timer = 1;

		_origin = transform.position;
		_target = _origin;
		_anchor = _origin;

		baseResBoxValuation = Random.value;
		_currentProfitExpectation = _startingProfitExpectation;
		_maxBuyingPriceMetric = MaxBuyingPrice;
		_minSellingPriceMetric = MinimumSellingPrice;
		_currentBuyingGoalMetric = CurrentBuyingPriceGoal;
		_currentSellingGoalMetric = CurrentSellingPriceGoal;
	}

	void Start()
	{
		SetIdling(true);
	}

	void Update()
	{
		Tick();
		RefreshMoneyLabel();

		if (IsFrozen)
			return;

		bool shouldHustle = _isIdling && !AreProfitsIncreasing && _restrategizingMoment < _totalSeconds;
		if (shouldHustle)
		{
			BusinessStrategy bestStrategy = CalculateBestStrategy();
			bool succesfullyStarted = TryStartStrategy(bestStrategy);

			if (!succesfullyStarted) // If we're stumped, idle for a bit longer until new opportunity arises
			{
				SetIdling(true);
				_restrategizingMoment = _totalSeconds + _opportunityWaitingTime;
			}
		}

		// Idling (no cost)
		if (_isIdling && Random.value < .001f)
		{
			_target.x = Mathf.Clamp(_anchor.x + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
			_target.z = Mathf.Clamp(_anchor.z + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
		}

		// Traveling
		float targetDist = Vector3.Distance(transform.position, _target);
		bool isBusinessTrip = !_isIdling;

		bool destinationReached = targetDist <= .001f;
		if (destinationReached)
		{
			if (isBusinessTrip)
			{
				_anchor = transform.position;

				if (_currentStrategy == BusinessStrategy.GATHERING)
				{
					SetIdling(true);
				}

				DestinationReached?.Invoke();
			}
		}
		else
		{
			bool resBoxDisappeared = isBusinessTrip && _currentStrategy == BusinessStrategy.GATHERING && _targetObject == null;
			if (resBoxDisappeared)
			{
				SetIdling(true);
			}

			Move();
		}
	}

	void Move()
	{
		Vector3 nextStep = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);

		bool isBusinessTrip = !_isIdling;
		if (isBusinessTrip)
		{
			float stepDist = Vector3.Distance(transform.position, nextStep);
			float travelCosts = stepDist * _gameManager.travelCostPerMeter;
			bool canAffordStep = travelCosts <= MoneyInWallet;

			if (!canAffordStep)
				return;

			AddMoney(-travelCosts);

			if (_currentStrategy == BusinessStrategy.GATHERING)
			{
				_totalGatheringTravelCosts += travelCosts;
			}
		}

		nextStep.y = _origin.y;
		transform.position = nextStep;
	}

	void SetIdling(bool idle)
	{
		if (idle)
		{
			Halt();

			_isIdling = true;
			_currentStrategy = BusinessStrategy.NONE;

			RefreshSecondaryLabels();
		}
		else
		{
			_isIdling = false;
		}
	}

	bool TryStartStrategy(BusinessStrategy strategy)
	{
		SetIdling(false);
		_anchor = transform.position;
		_currentStrategy = strategy;

		switch (strategy)
		{
			case BusinessStrategy.NONE:
				return false;
			case BusinessStrategy.GATHERING:
				return TryGatherResBox();
			case BusinessStrategy.BUYING:
				return TryBuying();
			case BusinessStrategy.SELLING:
				return TrySelling();
 			case BusinessStrategy.SERVICING:
				return false;
			default:
				return false;
		}
	}

	BusinessStrategy CalculateBestStrategy()
	{
		BusinessStrategy strategy = BusinessStrategy.NONE;
		float lowestCost = float.PositiveInfinity;

		// Gathering
		float gatheringCost = CalculateGatheringCost();
		if (gatheringCost <= MoneyInWallet && gatheringCost < lowestCost)
		{
			lowestCost = gatheringCost;
			strategy = BusinessStrategy.GATHERING;
		}

		// Buying
		if (CurrentBuyingPriceGoal <= MoneyInWallet)
		{
			float buyingCost = CalculateBuyingCost();
			if (buyingCost <= MoneyInWallet && buyingCost < lowestCost)
			{
				lowestCost = buyingCost;
				strategy = BusinessStrategy.BUYING;
			}
			else
			{
				_tradeInvestmentFraction = Mathf.Clamp(_tradeInvestmentFraction * 0.9f, 0f, 1f);
			}
		}

		// Selling
		if (totalResBoxesOwned > 0)
		{
			float sellingCost = CalculateSellingCost();
			if (sellingCost <= MoneyInWallet && sellingCost < lowestCost)
			{
				lowestCost = sellingCost;
				strategy = BusinessStrategy.SELLING;
			}
			else
			{
				_tradeInvestmentFraction = Mathf.Clamp(_tradeInvestmentFraction * 1.1f, 0f, 1f);
			}
		}

		return strategy;
	}

	#region Gathering Strategy
	bool TryGatherResBox()
	{
		var closestBox = GetClosestResBox();
		if (closestBox == null)
			return false;

		_targetObject = closestBox.gameObject;
		_target = closestBox.transform.position;
		_target.y = _origin.y;

		return true;
	}

	float CalculateGatheringCost()
	{
		var closestBox = GetClosestResBox();
		if (closestBox == null)
			return float.PositiveInfinity; // maybe not the most future-proof output, but it should work for now

		float dist = Vector3.Distance(_anchor, closestBox.transform.position);

		return dist * _gameManager.travelCostPerMeter;
	}

	ResBox GetClosestResBox()
	{
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length <= 0)
		{
			return null;
		}

		ResBox closestBox = boxes[0];
		float closestDist = Vector3.Distance(_anchor, boxes[0].transform.position);
		foreach (var box in boxes)
		{
			float dist = Vector3.Distance(_anchor, box.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				closestBox = box;
			}
		}

		return closestBox;
	}
	#endregion

	#region Trading Strategy
	bool TryBuying()
	{
		_currentBuyingGoalMetric = CurrentBuyingPriceGoal;

		if (CurrentBuyingPriceGoal > MoneyInWallet)
			return false;

		if (IsFrozen) // shouldnt happen but just in case
			return false;

		if (!TryFindTargetCitizen())
			return false;

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other.TryAskForAttention())
		{
			DestinationReached += AskToBuy;
			return true;
		}

		return false;
	}

	void AskToBuy()
	{
		if (_currentStrategy != BusinessStrategy.BUYING)
		{
			DestinationReached -= AskToBuy;
			return;
		}

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other == null)
			return;

		bool isOtherBusy = other.CurrentStrategy != BusinessStrategy.NONE && other.CurrentStrategy != BusinessStrategy.SELLING;
		if (isOtherBusy)
			return;

		other.ReleaseAttention();
		other.Halt(3f);

		Halt(3f);

		float unitOffer = CurrentBuyingPriceGoal;
		int buyAmount = Mathf.CeilToInt(MoneyInWallet * _tradeInvestmentFraction / unitOffer);

		Debug.Log($"<color=lime><b>{name}</b> wants to <b>buy {buyAmount} {GameManager.ResBoxSymbol}</b> from <b>{other.name}</b> for <b>{GameManager.FormatMoney(unitOffer * buyAmount)}</b>...</color>");

		bool success = other.TryBuyBox(buyAmount, unitOffer, this);

		if (success)
		{
			_currentProfitExpectation *= 1.1f; // Business is good, increase profit expectations
		}
		else
		{
			_currentProfitExpectation *= 0.9f; // Business sucks, decrease profit expectations
			_lastFailedTradePartner = other;
		}

		SetIdling(true);

		DestinationReached -= AskToBuy;
	}

	bool TrySelling()
	{
		_currentSellingGoalMetric = CurrentSellingPriceGoal;

		if (totalResBoxesOwned <= 0)
			return false;

		if (IsFrozen)
			return false;

		if (TryFindTargetCitizen())
		{
			Citizen other = _targetObject.GetComponent<Citizen>();
			if (other.TryAskForAttention())
			{
				DestinationReached += AskToSell;
				return true;
			}
		}

		return false;
	}

	void AskToSell()
	{
		if (_currentStrategy != BusinessStrategy.SELLING)
		{
			DestinationReached -= AskToSell;
			return;
		}

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other == null)
			return;

		bool isOtherBusy = other.CurrentStrategy != BusinessStrategy.NONE && other.CurrentStrategy != BusinessStrategy.BUYING;
		if (isOtherBusy)
			return;

		other.ReleaseAttention();
		other.Halt(3f);

		Halt(3f);

		float unitOffer = CurrentSellingPriceGoal;
		int sellAmount = Mathf.CeilToInt(totalResBoxesOwned * (1 - _tradeInvestmentFraction));
		bool success = other.TrySellBox(sellAmount, unitOffer, this);

		if (success)
		{
			//_currentProfitExpectation *= 1.1f; // Business is good, increase profit expectations
		}
		else
		{
			//_currentProfitExpectation *= 0.9f; // Business sucks, decrease profit expectations
			_lastFailedTradePartner = other;
		}

		SetIdling(true);

		DestinationReached -= AskToSell;
	}

	bool TryNegotiateTradeDeal(int startingAmount, out int finalAmount, float unitOffer, Citizen buyer, out float finalTotalPrice, bool isFinal = false)
	{
		finalTotalPrice = float.PositiveInfinity;
		finalAmount = startingAmount;

		string logString = "";

		/*
		 * Can the seller even handle this order?
		 */
		if (startingAmount <= 0)
			return false;

		if (startingAmount > totalResBoxesOwned)
		{
			logString += $"\n{name} can't sell {startingAmount} {GameManager.ResBoxSymbol} to {buyer.name} because the order is <b>too big</b>.";

			if (totalResBoxesOwned > 1)
			{
				logString += "\nLet's try half the amount.";
				int newAmount = Mathf.RoundToInt(startingAmount / 2);
				return TryNegotiateTradeDeal(newAmount, out finalAmount, unitOffer, buyer, out finalTotalPrice);
			}

			if (totalResBoxesOwned == 1) // Does the seller have a box at all?
			{
				logString += "\nLet's try just ONE box.";
				return TryNegotiateTradeDeal(1, out finalAmount, unitOffer, buyer, out finalTotalPrice, true);
			}

			Debug.Log(logString);
			return false;
		}

		/*
		 * A first price is drafted that favors the seller, if possible.
		 */
		float fairestUnitPrice = MinimumSellingPrice + ((unitOffer - MinimumSellingPrice) / 2);
		float initialUnitPrice = unitOffer > fairestUnitPrice ? unitOffer : fairestUnitPrice;
		float buyerMaxTotalPrice = startingAmount * buyer.MaxBuyingPrice;
		float sellerMinimumTotalPrice = startingAmount * MinimumSellingPrice;
		finalTotalPrice = isFinal ? unitOffer : startingAmount * initialUnitPrice;

		if (isFinal)
			logString += $"\nThe final offer is <b>{GameManager.FormatMoney(finalTotalPrice)}</b>...";
		else
			logString += $"\nThe negotation starts at <b>{GameManager.FormatMoney(finalTotalPrice)}</b>...";

		/*
		 * If the customer can't afford that, the order amount could be lowered or the seller could try to make a final, minimum offer.
		 */
		if (!buyer.CanPay(finalTotalPrice))
		{
			logString += $"\n{buyer.name} <b>can't afford</b> {GameManager.FormatMoney(finalTotalPrice)}...";

			/*
			 * First, let's try lowering the amount.
			 */
			if (startingAmount > 1)
			{
				logString += "\nLet's try half the amount.";
				int newAmount = Mathf.RoundToInt(startingAmount / 2);
				return TryNegotiateTradeDeal(newAmount, out finalAmount, unitOffer, buyer, out finalTotalPrice);
			}

			/*
			 * If that doesn't cut it, the seller will make his final offer for 1 box.
			 */
			float finalOffer = sellerMinimumTotalPrice;

			logString += $"\n{name} offers a <b>final deal</b> of {finalAmount} {GameManager.ResBoxSymbol} to {buyer.name} for <b>{GameManager.FormatMoney(finalOffer)}</b>...";

			/*
			 * If the customer can afford the final offer, that will be the deal.
			 */
			if (!buyer.CanPay(finalOffer))
			{
				logString += $"\n{buyer.name} still <b>can't afford</b> it.";

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
			logString += $"\n{buyer.name} <b>won't buy</b> {finalAmount} {GameManager.ResBoxSymbol} from {name} for {GameManager.FormatMoney(finalTotalPrice)} because it's <b>not worth it</b>.";

			if (isFinal)
			{
				Debug.Log(logString);
				return false;
			}
			else return TryNegotiateTradeDeal(startingAmount, out finalAmount, buyerMaxTotalPrice, buyer, out finalTotalPrice, true);
		}

		if (finalTotalPrice < sellerMinimumTotalPrice)
		{
			logString += $"\n{name} <b>won't sell</b> {finalAmount} {GameManager.ResBoxSymbol} to {buyer.name} for {GameManager.FormatMoney(finalTotalPrice)} because it's <b>not worth it</b>.";

			if (isFinal)
			{
				Debug.Log(logString);
				return false;
			}
			else return TryNegotiateTradeDeal(startingAmount, out finalAmount, sellerMinimumTotalPrice, buyer, out finalTotalPrice, true);
		}

		Debug.Log(logString);
		return true;
	}

	bool TryFindTargetCitizen()
	{
		var closestCitizen = GetClosestCitizen();
		if (closestCitizen == null)
			return false;

		_targetObject = closestCitizen.gameObject;
		_target = closestCitizen.transform.position;
		_target.y = _origin.y;

		// Small offset so not to run into eachother
		Vector3 dir = (_target - transform.position).normalized;
		_target -= dir * 2;

		return true;
	}

	float CalculateBuyingCost()
	{
		float citizenFindingCost = CalculateCitizenFindingCost();
		int buyAmount = Mathf.CeilToInt(MoneyInWallet * _tradeInvestmentFraction / CurrentBuyingPriceGoal);
		float buyingCost = CurrentBuyingPriceGoal * buyAmount;
		float incentive = TotalProfits / ProfitPerSecond;
		float totalCost = citizenFindingCost + buyingCost - incentive;

		return Mathf.Max(citizenFindingCost, totalCost);
	}

	float CalculateSellingCost()
	{
		float citizenFindingCost = CalculateCitizenFindingCost();
		int sellAmount = Mathf.CeilToInt(totalResBoxesOwned * _tradeInvestmentFraction);
		float sellingCost = MinimumSellingPrice * sellAmount;
		float incentive = TotalProfits / ProfitPerSecond;
		float totalCost = citizenFindingCost + sellingCost - incentive;

		return Mathf.Max(citizenFindingCost, totalCost);
	}

	float CalculateCitizenFindingCost()
	{
		var closestCitizen = GetClosestCitizen();
		if (closestCitizen == null)
			return float.PositiveInfinity; // maybe not the most future-proof output, but it should work for now

		float dist = Vector3.Distance(_anchor, closestCitizen.transform.position);

		return dist * _gameManager.travelCostPerMeter;
	}

	Citizen GetClosestCitizen()
	{
		var citizens = FindObjectsOfType<Citizen>();
		if (citizens.Length <= 0)
		{
			return null;
		}

		Citizen closestCitizen = citizens[0] == this || citizens[0] == _lastFailedTradePartner ? citizens[1] : citizens[0];
		float closestDist = Vector3.Distance(_anchor, citizens[0].transform.position);
		foreach (var citizen in citizens)
		{
			if (citizen == this || citizen == _lastFailedTradePartner)
				continue;

			float dist = Vector3.Distance(_anchor, citizen.transform.position);
			if (dist < closestDist)
			{
				closestDist = dist;
				closestCitizen = citizen;
			}
		}

		return closestCitizen;
	}

	void SellResBox(int amount, float totalPrice, Citizen buyer)
	{
		if (amount > totalResBoxesOwned)
		{
			Debug.LogWarning($"Warning: {name} tried to sell {amount} {GameManager.ResBoxSymbol}, but only has {totalResBoxesOwned} {GameManager.ResBoxSymbol}!");
			return;
		}

		// Bookkeeping
		float profit = totalPrice - MinimumSellingPrice * amount;

		_maxBuyingPriceMetric = MaxBuyingPrice;
		_minSellingPriceMetric = MinimumSellingPrice;

		RegisterRevenue(totalPrice, profit);
		_gameManager.RegisterSale(totalPrice, amount);

		// Transaction
		totalResBoxesOwned -= amount;
		AddMoney(totalPrice);
		buyer.AddMoney(-totalPrice);
		buyer.totalResBoxesOwned += amount;

		Debug.Log($"<color=yellow><b>{name}</b> sold <b>{amount} {GameManager.ResBoxSymbol}</b> to <b>{buyer.name}</b> for <b>{GameManager.FormatMoney(totalPrice)}</b>!</color>");
	}
	#endregion

	#region Bookkeeping
	void RegisterRevenue(float revenue, float profit)
	{
		RegisterProfit(profit);

		float profitMargin = profit / revenue;
		_latestProfitMargin = profitMargin;
	}

	void RegisterProfit(float profit)
	{
		_profitGrowth = _latestProfit == 0 ? 1 : (profit / _latestProfit - 1);
		_latestProfit = profit;

		// Good business makes us care slightly more about unit value, and vice-versa
		baseResBoxValuation = Mathf.Max(0f, baseResBoxValuation * (1 + (_profitGrowth / 10)));

		RefreshSecondaryLabels();
	}
	#endregion

	void Tick()
	{
		_timer -= Time.deltaTime;

		if (_timer <= 0)
		{
			_timer = 1;
			_totalSeconds++;
		}
	}

	void RefreshMoneyLabel()
	{
		moneyLabel.text = $"{GameManager.FormatMoney(MoneyInWallet)}";
	}

	void RefreshSecondaryLabels()
	{
		string boxStatus = $"{totalResBoxesOwned} {GameManager.ResBoxSymbol}";

		secondLabel.text = boxStatus;
		secondLabel.color = new Color(0f, 0.75f, 1f);

		float profitValue = _latestProfitMargin;
		string profitStatus = profitValue == 0 ? "-" : $"{(profitValue > 0 ? "+" : "")}{profitValue * 100:n2}%";

		thirdLabel.text = profitStatus;
		thirdLabel.color = profitValue < 0 ? Color.red : Color.green;
	}

	void OnTriggerEnter(Collider collision)
	{
		var box = collision.gameObject.GetComponent<ResBox>();
		if (box != null && !_isIdling)
		{
			totalResBoxesOwned++;
			_totalResBoxesGathered++;
			Destroy(collision.gameObject);

			_maxBuyingPriceMetric = MaxBuyingPrice;
			_minSellingPriceMetric = MinimumSellingPrice;

			secondLabel.text = $"+1 {GameManager.ResBoxSymbol}";
			secondLabel.color = Color.cyan;
		}
	}
}
