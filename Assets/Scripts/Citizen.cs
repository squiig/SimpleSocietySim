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
		TRADING,
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
	public TMP_Text secondaryLabel;

	[SerializeField] float _speed = 5f;
	[SerializeField] float _fieldRadius = 20f;
	[SerializeField] float _idlingRadius = .5f;

	public bool IsFrozen;

	[Header("Interesting Stuff")]
	public float baseResBoxValuation; // starts out random, but can fluctuate by effect of profit margins
	public int totalResBoxesOwned; // the primary asset that can be found or bought and used as merchandise

	[SerializeField] float _opportunityWaitingTime = 2f;
	[SerializeField] float _minimumProfitExpectation = 0.01f;
	[SerializeField] float _startingProfitExpectation = 0.1f;

	[Header("Read-Only")]
	[SerializeField] float _liquidMoney;
	[SerializeField] float _maxBuyingPriceMetric;
	//[SerializeField] bool _isDoomed; // meaning they dont have an existing cashflow + dont own assets + cant afford any type of investment right now
	[SerializeField] bool _isIdling; // just for aesthetic effect, doesn't cost money
	[SerializeField] BusinessStrategy _currentStrategy;
	//[SerializeField] bool _wantsToTrade;
	[SerializeField] int _totalResBoxesGathered;
	[SerializeField] float _totalGatheringTravelCosts;
	[SerializeField] float _latestProfit;
	[SerializeField] float _latestProfitMargin;
	[SerializeField] float _profitGrowth;
	[SerializeField] float _currentProfitExpectation;
	[SerializeField] float _currentBuyingGoalMetric;

	public float MoneyInWallet => _liquidMoney;

	public float AverageResBoxAcquisitionCost => _totalGatheringTravelCosts / _totalResBoxesGathered;

	// Never goes below the citizen's base valuation of one res box
	public float MinimumSellingPrice => AverageResBoxAcquisitionCost + Mathf.Max(baseResBoxValuation, baseResBoxValuation / (totalResBoxesOwned + 1) * _gameManager.priceMagnifier);

	public float MaxBuyingPrice => MinimumSellingPrice * (1 - _minimumProfitExpectation); // wants at the very least X% profit potential

	public float CurrentBuyingPriceGoal => MinimumSellingPrice * (1 - _currentProfitExpectation); // aiming for a X% profit potential

	public float ProfitGrowth => _profitGrowth;

	public bool AreProfitsIncreasing => ProfitGrowth > 0;

	public float TotalProfits => _liquidMoney - _gameManager.citizenStartingCapital;

	public float ProfitPerSecond => TotalProfits / _totalSeconds;

	public BusinessStrategy CurrentStrategy => _currentStrategy;

	public event Action DestinationReached;

	public void AddMoney(float money)
	{
		_liquidMoney += money;
	}

	public bool TryBuyResBox(int amount, float unitOffer, Citizen customer)
	{
		Debug.Log($"{customer.name} wants to buy {amount} box from {name} for ƒ{unitOffer*amount:n2}...");

		// Considerations
		float totalPrice;
		bool isDealSuccessful = TryNegotiateTradeDeal(amount, unitOffer, customer, out totalPrice);

		if (!isDealSuccessful)
		{
			Debug.Log("Trade canceled.");
			return false;
		}

		// Perform the sale
		SellResBox(amount, totalPrice, customer);

		return true;
	}

	public bool CanPay(float price)
	{
		return price <= _liquidMoney;
	}

	public void Halt(float time = 0f)
	{
		_anchor = transform.position;
		_target = _anchor;
		_targetObject = null;
		DestinationReached = null;
		_restrategizingMoment = _totalSeconds + time;
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
		_currentBuyingGoalMetric = CurrentBuyingPriceGoal;
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

			if (bestStrategy != BusinessStrategy.NONE)
			{
				StartStrategy(bestStrategy);
			}
			else // If we're stumped, idle for a bit longer until new opportunity arises
			{
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
			bool canAffordStep = travelCosts <= _liquidMoney;

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

			RefreshSecondaryLabel();
		}
		else
		{
			_isIdling = false;
		}
	}

	void StartStrategy(BusinessStrategy strategy)
	{
		SetIdling(false);
		_anchor = transform.position;
		_currentStrategy = strategy;

		switch (strategy)
		{
			case BusinessStrategy.NONE:
				break;
			case BusinessStrategy.GATHERING:
				TryGatherResBox();
				break;
			case BusinessStrategy.TRADING:
				TryTrading();
				break;
			case BusinessStrategy.SERVICING:
				break;
			default:
				break;
		}
	}

	BusinessStrategy CalculateBestStrategy()
	{
		BusinessStrategy strategy = BusinessStrategy.NONE;
		float lowestCost = float.PositiveInfinity;

		float gatheringCost = CalculateGatheringCost();
		if (gatheringCost <= _liquidMoney && gatheringCost < lowestCost)
		{
			lowestCost = gatheringCost;
			strategy = BusinessStrategy.GATHERING;
		}

		if (totalResBoxesOwned > 0)
		{
			float tradingCost = CalculateTradingCost();
			if (tradingCost <= _liquidMoney && tradingCost < lowestCost)
			{
				lowestCost = tradingCost;
				strategy = BusinessStrategy.TRADING;
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
	bool TryTrading()
	{
		_currentBuyingGoalMetric = CurrentBuyingPriceGoal;

		if (IsFrozen)
			return false;

		bool foundTargetCitizen = TryFindTargetCitizen();
		if (foundTargetCitizen)
		{
			Citizen other = _targetObject.GetComponent<Citizen>();
			other.IsFrozen = true;
			DestinationReached += AttemptTrading;
			return true;
		}

		return false;
	}

	void AttemptTrading()
	{
		if (_currentStrategy != BusinessStrategy.TRADING)
		{
			DestinationReached -= AttemptTrading;
			return;
		}

		Citizen other = _targetObject.GetComponent<Citizen>();
		if (other == null)
			return;

		bool isOtherBusy = !(other.CurrentStrategy == BusinessStrategy.NONE || other.CurrentStrategy == BusinessStrategy.TRADING);
		if (isOtherBusy)
			return;

		other.IsFrozen = false;
		other.Halt(3f);

		Halt(3f);

		int buyAmount = 1;
		float offer = CurrentBuyingPriceGoal;
		bool success = other.TryBuyResBox(buyAmount, offer, this);

		if (success)
		{
			_currentProfitExpectation *= 1.1f; // Business is good, increase profit expectations
		}
		else
		{
			_currentProfitExpectation *= 0.8f; // Business sucks, decrease profit expectations
		}

		SetIdling(true);

		DestinationReached -= AttemptTrading;
	}

	bool TryNegotiateTradeDeal(int amount, float unitOffer, Citizen customer, out float currentTotalPrice)
	{
		currentTotalPrice = float.PositiveInfinity;

		/*
		 * Can the seller even handle this order?
		 */
		if (totalResBoxesOwned < amount)
		{
			Debug.Log($"{name} can't sell {amount} box(es) to {customer.name} because the order is too big.");
			return false;
		}

		/*
		 * A first price is drafted that's fair to both.
		 */
		float initialUnitPrice = MinimumSellingPrice + ((unitOffer - MinimumSellingPrice) / 2); // Just compromise in the middle, keep it simple for now
		float buyerMaxTotalPrice = amount * customer.MaxBuyingPrice;
		float sellerMinimumTotalPrice = amount * MinimumSellingPrice;
		currentTotalPrice = amount * initialUnitPrice;

		Debug.Log($"{name} is considering to sell {amount} box(es) to {customer.name} at ƒ{currentTotalPrice:n2}...");

		/*
		 * If the customer can't afford that, the seller will try to make a final, minimum offer.
		 */
		if (!customer.CanPay(currentTotalPrice))
		{
			Debug.Log($"{customer.name} can't afford ƒ{currentTotalPrice:n2}...");

			float finalOffer = sellerMinimumTotalPrice;

			Debug.Log($"{name} offers a final deal of {amount} box(es) to {customer.name} for ƒ{finalOffer:n2}...");

			/*
			 * If the customer can afford the final offer, that will be the deal.
			 */
			if (!customer.CanPay(finalOffer))
			{
				Debug.Log($"{customer.name} still can't afford it.");
				return false; // Otherwise the order will have to be canceled altogether
			}

			currentTotalPrice = finalOffer;
		}

		/*
		 * After it's determined if the buyer can pay, both will decide if it's worth it to them.
		 */
		if (currentTotalPrice < buyerMaxTotalPrice)
		{
			Debug.Log($"{customer.name} won't buy {amount} box(es) from {name} for ƒ{currentTotalPrice:n2} because it's not worth it.");
			return false;
		}

		if (currentTotalPrice < sellerMinimumTotalPrice)
		{
			Debug.Log($"{name} won't sell {amount} box(es) to {customer.name} for ƒ{currentTotalPrice:n2} because it's not worth it.");
			return false;
		}

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

	float CalculateTradingCost()
	{
		return CalculateMerchantFindingCost() + MaxBuyingPrice;
	}

	float CalculateMerchantFindingCost()
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

		Citizen closestCitizen = citizens[0] == this ? citizens[1] : citizens[0];
		float closestDist = Vector3.Distance(_anchor, citizens[0].transform.position);
		foreach (var citizen in citizens)
		{
			if (citizen == this)
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

	void SellResBox(int amount, float totalPrice, Citizen customer)
	{
		// Transaction
		totalResBoxesOwned -= amount;
		AddMoney(totalPrice);
		customer.AddMoney(-totalPrice);
		customer.totalResBoxesOwned += amount;

		Debug.Log($"{name} sold {amount} B to {customer.name} for ƒ{totalPrice:n2}!");

		_maxBuyingPriceMetric = MaxBuyingPrice;

		// Bookkeeping
		float profit = totalPrice - MinimumSellingPrice * amount;
		RegisterRevenue(totalPrice, profit);
		_gameManager.RegisterConsumption(totalPrice);
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
		_profitGrowth = profit / (_latestProfit == 0 ? profit : _latestProfit) - 1;
		_latestProfit = profit;

		// Good business makes us care slightly more about unit value, and vice-versa
		baseResBoxValuation *= 1 + (_profitGrowth / 10);

		RefreshSecondaryLabel();
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
		moneyLabel.text = $"ƒ{_liquidMoney:n2}";
	}

	void RefreshSecondaryLabel()
	{
		secondaryLabel.text = ProfitGrowth == 0 ? $"{totalResBoxesOwned} boxes" : $"{(AreProfitsIncreasing ? "+" : "") + (ProfitGrowth * 100):n2}%";
		secondaryLabel.color = ProfitGrowth == 0 ? new Color(0f, 0.5f, 1f) : (AreProfitsIncreasing ? Color.green : Color.red);
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

			secondaryLabel.text = "+1 box";
			secondaryLabel.color = Color.cyan;
		}
	}
}
