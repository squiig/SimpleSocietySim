using TMPro;
using UnityEngine;

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

	public TMP_Text moneyText;
	public TMP_Text secondaryText;
	public float baseResBoxValuation; // starts out random, but can fluctuate by effect of profit margins
	public float maxBuyingPrice;
	public int totalResBoxesOwned; // the primary asset that can be found or bought

	[SerializeField] float _speed = 5f;
	[SerializeField] float _fieldRadius = 20f;
	[SerializeField] float _idlingRadius = 2f;
	[SerializeField] float _opportunityWaitingTime = 2f;

	[Header("Read-Only")]
	[SerializeField] float _liquidMoney;
	//[SerializeField] bool _isDoomed; // meaning they dont have an existing cashflow + dont own assets + cant afford any type of investment right now
	[SerializeField] bool _isIdling; // just for aesthetic effect, doesn't cost money
	[SerializeField] BusinessStrategy _currentStrategy;
	[SerializeField] int _totalResBoxesGathered;
	[SerializeField] float _totalGatheringTravelCosts;
	[SerializeField] float _latestProfit;
	[SerializeField] float _latestProfitMargin;
	[SerializeField] float _profitGrowth;

	public float AverageResBoxAcquisitionCost => _totalGatheringTravelCosts / _totalResBoxesGathered;

	// Never goes below the citizen's base valuation of one res box
	public float MinimumResBoxSellingPrice => Mathf.Max(baseResBoxValuation, baseResBoxValuation / (totalResBoxesOwned + 1) * _gameManager.priceMagnifier);

	public float ProfitGrowth => _profitGrowth;

	public bool AreProfitsIncreasing => ProfitGrowth > 0;

	public float TotalProfits => _liquidMoney - _gameManager.citizenStartingCapital;

	public float ProfitPerSecond => TotalProfits / _totalSeconds;

	public void AddMoney(float money)
	{
		_liquidMoney += money;
	}

	void Start()
	{
		_gameManager = FindObjectOfType<GameManager>();

		_timer = 1;

		_origin = transform.position;
		_target = _origin;

		baseResBoxValuation = Random.value;
		maxBuyingPrice = baseResBoxValuation * 1.1f;

		SetIdling(true);
	}

	void Update()
	{
		Tick();
		RefreshHUD();

		bool shouldHustle = _isIdling && !AreProfitsIncreasing && _restrategizingMoment < _totalSeconds;
		if (shouldHustle)
		{
			BusinessStrategy bestStrategy = CalculateBestStrategy();

			if (bestStrategy != BusinessStrategy.NONE)
			{
				StartStrategy(bestStrategy);
			}
			else // if we're stumped, idle for a bit longer until new opportunity arises
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
				SetIdling(true);
			}
		}
		else
		{
			bool businessTargetDisappeared = isBusinessTrip && _currentStrategy == BusinessStrategy.GATHERING && _targetObject == null;
			if (businessTargetDisappeared)
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
			_isIdling = true;
			_anchor = transform.position;
			_target = _anchor;
			_currentStrategy = BusinessStrategy.NONE;

			secondaryText.text = $"{totalResBoxesOwned} boxes";
			secondaryText.color = new Color(0f, 0.6f, 1f);
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

		float gatheringCost = GetGatheringCost();
		if (gatheringCost <= _liquidMoney && gatheringCost < lowestCost)
		{
			lowestCost = gatheringCost;
			strategy = BusinessStrategy.GATHERING;
		}

		// more strategies here

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

	float GetGatheringCost()
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
	public bool TryBuyResBox(int amount, float unitOffer, Citizen customer)
	{
		// Considerations
		if (totalResBoxesOwned < amount) // can i sell that much?
			return false;

		float unitPrice = MinimumResBoxSellingPrice + ((unitOffer - MinimumResBoxSellingPrice) / 2); // just compromise in the middle, keep it simple for now
		float totalPrice = amount * unitPrice;

		float minimumTotalPrice = MinimumResBoxSellingPrice * amount;
		if (totalPrice < minimumTotalPrice) // would i lose value?
			return false;

		// Perform the sale
		SellResBox(amount, totalPrice, customer);

		return true;
	}

	void SellResBox(int amount, float totalPrice, Citizen customer)
	{
		// Transaction
		totalResBoxesOwned -= amount;
		AddMoney(totalPrice);
		customer.AddMoney(-totalPrice);
		customer.totalResBoxesOwned += amount;

		// Bookkeeping
		float profit = totalPrice - MinimumResBoxSellingPrice * amount;
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

		// Display the profit
		secondaryText.text = ProfitGrowth == 0 ? "" : $"{(AreProfitsIncreasing ? "+" : "") + (ProfitGrowth * 100):n2}%";
		secondaryText.color = AreProfitsIncreasing ? Color.green : Color.red;
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

	void RefreshHUD()
	{
		moneyText.text = $"ƒ{_liquidMoney:n2}";
	}

	void OnTriggerEnter(Collider collision)
	{
		var box = collision.gameObject.GetComponent<ResBox>();
		if (box != null && !_isIdling)
		{
			totalResBoxesOwned++;
			_totalResBoxesGathered++;
			Destroy(collision.gameObject);

			secondaryText.text = "+1 box";
			secondaryText.color = Color.cyan;
		}
	}
}
