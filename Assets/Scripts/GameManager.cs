using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
	/// <summary>
	/// Represents a real-time snapshot of the total nominal GDP.
	/// </summary>
	public float AllTimeNominalGDP => _totalExpenditures;

	public float PeriodicalNominalGDP => _gdpPerFP;

	public static string CurrencySymbol => "€"; // ƒ

	public static string ResBoxSymbol => "BOX";

	public float CurrentAverageResBoxTradingPrice => _historicalUnitPrices.Count == 0 ? 0f : _historicalUnitPrices.Sum() / _historicalUnitPrices.Count;

	public float CurrentAverageResBoxValuation => _citizens.Select(x => x.BaseResBoxValuation).Sum() / _citizens.Count;

	public float PriceMagnifier => _priceMagnifier;

	public float TravelCostPerMeter => _travelCostPerMeter;

	public BoxVendor BoxVendor { get => _boxVendor; set => _boxVendor = value; }
	public float FinancialPeriodInSeconds => _financialPeriodInSeconds;

	[SerializeField] private GameObject _resBoxPrefab;
	[SerializeField] private GameObject _citizenPrefab;
	[SerializeField] private TMP_Text _populationLabel;
	[SerializeField] private TMP_Text _totalMoneyLabel;
	[SerializeField] private TMP_Text _averageValuationLabel;
	[SerializeField] private TMP_Text _averageTradingPriceLabel;
	[SerializeField] private BoxVendor _boxVendor;

	[Header("Settings")]
	[SerializeField] private float _fieldRadius = 25f;
	[SerializeField] private int _citizenSpawnCount = 3;
	[SerializeField] private float _citizenStartingMoneyMin = 10f;
	[SerializeField] private float _citizenStartingMoneyMax = 100f;
	[SerializeField] private float _priceMagnifier = 100f;
	[SerializeField] private float _travelCostPerMeter = 1f;
	[Space]
	[SerializeField] private int _resBoxSpawnCount = 80;
	[SerializeField] private bool _enableResBoxRespawning = true;
	[SerializeField] private float _resBoxRespawnPeriodInSeconds = 10f;
	[SerializeField, ReadOnly] private float _resBoxRespawnTimer;
	[SerializeField] private int _citizenMinStartingResBoxes = 0;
	[SerializeField] private int _citizenMaxStartingResBoxes = 10;

	[Header("Metrics")]
	[SerializeField, ReadOnly] private int _totalResBoxesInMarket;
	[SerializeField, ReadOnly] private float _totalExpenditures;
	[Space]
	[SerializeField] private float _financialPeriodInSeconds = 5f;
	[SerializeField, ReadOnly] private float _financialPeriodTimer;
	[SerializeField, ReadOnly] private float _gdpPerFP;
	[SerializeField, ReadOnly] private float _previousMeasuredGdp;
	[SerializeField] private RunChart _nominalGdpChart;
	[SerializeField] private RunChart _nominalGdpPerCapitaChart;
	[SerializeField, ReadOnly] private float _avgProfitsPerFP;
	[SerializeField] private RunChart _avgProfitsChart;
	[Space]
	[SerializeField, ReadOnly] private float _boxMetricsTimer;
	[SerializeField] private float _boxMetricsPeriodInSeconds = 5f;
	[SerializeField] private RunChart _avgResBoxTradingPriceChart;

	private List<Citizen> _citizens;
	private List<float> _historicalUnitPrices;

	public static string FormatMoney(float amount)
	{
		return $"{CurrencySymbol}{amount:n2}";
	}

	public void RegisterSale(float amountSpent, int amountSold)
	{
		_totalExpenditures += amountSpent;

		float unitPrice = amountSpent / amountSold;

		_historicalUnitPrices.Add(unitPrice);
	}

	void Awake()
	{
		_historicalUnitPrices = new List<float>();
	}

	// Start is called before the first frame update
	void Start()
	{
		//_totalExpenditures = citizenSpawnCount * citizenStartingCapital; // the only bit of "government spending" to account into GDP for now
		_totalResBoxesInMarket = _resBoxSpawnCount;

		SpawnInitialCitizens();
		SpawnResBoxes(_resBoxSpawnCount);

		RefreshCitizenMetrics();
		RefreshTotalMoneyLabel();
	}

	void RefreshCitizenMetrics()
	{
		_populationLabel.text = $"Population: {_citizens.Count}";
	}

	void RefreshTotalMoneyLabel()
	{
		float totalMoney = _citizens.Select(x => x.MoneyInWallet).Sum();
		_totalMoneyLabel.text = $"{FormatMoney(totalMoney)} total";
	}

	void RefreshAverages()
	{
		_averageTradingPriceLabel.text = $"{FormatMoney(CurrentAverageResBoxTradingPrice)} avg. price";
		_averageValuationLabel.text = $"{CurrentAverageResBoxValuation:n3} avg. valuation";
	}

	void SpawnInitialCitizens()
	{
		_citizens = new List<Citizen>();
		for (int i = 0; i < _citizenSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, 1f, 0f);
			target.x = Random.value * _fieldRadius * 2 - _fieldRadius;
			target.z = Random.value * _fieldRadius * 2 - _fieldRadius;
			SpawnCitizen(target, $"Citizen {i + 1}", Color.HSVToRGB(1f / _citizenSpawnCount * (i + 1), 1f, 1f));
		}
	}

	void SpawnCitizen(Vector3 pos, string name, Color color)
	{
		var go = Instantiate(_citizenPrefab, pos, Quaternion.identity);
		go.name = name;
		Citizen citizen = go.GetComponent<Citizen>();
		citizen.GetComponent<Renderer>().material.color = color;
		float startingMoney = Random.Range(_citizenStartingMoneyMin, _citizenStartingMoneyMax);
		int startingBoxes = Random.Range(_citizenMinStartingResBoxes, _citizenMaxStartingResBoxes);
		citizen.GiveStartingCapital(startingMoney, startingBoxes);
		_totalResBoxesInMarket += startingBoxes;
		_citizens.Add(citizen);
	}

	void SpawnResBox(Vector3 pos, Quaternion rot)
	{
		Instantiate(_resBoxPrefab, pos, rot);
	}

	void SpawnResBox(Vector3 pos)
	{
		Quaternion rot = Random.rotation;
		rot.x = 0f;
		rot.z = 0f;
		SpawnResBox(pos, rot);
	}

	void SpawnResBoxes(int amount)
	{
		for (int i = 0; i < amount; i++)
		{
			Vector3 target = new Vector3(0f, 100f, 0f);
			target.x = Random.value * _fieldRadius * 2 - _fieldRadius;
			target.z = Random.value * _fieldRadius * 2 - _fieldRadius;
			SpawnResBox(target, Random.rotation);
		}
	}

	void TickFinancialPeriodTimer()
	{
		_financialPeriodTimer += Time.deltaTime;
		if (_financialPeriodTimer >= _financialPeriodInSeconds)
		{
			_financialPeriodTimer = 0;

			UpdateFinancialMetrics();
			RefreshCitizenMetrics();
		}
	}

	void UpdateFinancialMetrics()
	{
		// GDP stuff
		_gdpPerFP = AllTimeNominalGDP - _previousMeasuredGdp;
		_previousMeasuredGdp = AllTimeNominalGDP;

		_nominalGdpChart.AddValue(_gdpPerFP);
		_nominalGdpPerCapitaChart.AddValue(_gdpPerFP / _citizens.Count);

		// Other
		_avgProfitsPerFP = _citizens.Select(x => x.HistoricalPeriodProfits.LastOrDefault()).Average();
		_avgProfitsChart.AddValue(_avgProfitsPerFP);
	}

	void UpdateBoxPriceMetrics()
	{
		_boxMetricsTimer += Time.deltaTime;
		if (_boxMetricsTimer >= _boxMetricsPeriodInSeconds)
		{
			_boxMetricsTimer = 0;

			// Chart stuff
			float val = CurrentAverageResBoxTradingPrice;
			_avgResBoxTradingPriceChart.AddValue(val);
			//Debug.LogWarning(val);
		}
	}

	void UpdateBoxRespawnClock()
	{
		_resBoxRespawnTimer += Time.deltaTime;
		if (_resBoxRespawnTimer >= _resBoxRespawnPeriodInSeconds)
		{
			_resBoxRespawnTimer = 0;
			SpawnResBoxes(Mathf.RoundToInt(Random.value * (_resBoxSpawnCount / 2)));
		}
	}

	void ListenForCitizenSpawnInput()
	{
        if (Input.GetMouseButtonUp(0))
		{
			RaycastHit hit;
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, Camera.main.farClipPlane, ~LayerMask.NameToLayer("Floor")))
			{
				Vector3 pos = hit.point;
				pos.y = 1f;
				SpawnCitizen(pos, $"Citizen {_citizens.Count + 1} (new)", Color.white);
			}
		}
	}

	void ListenForBoxSpawnInput()
	{
		if (Input.GetMouseButtonUp(1))
		{
			RaycastHit hit;
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, Camera.main.farClipPlane, ~LayerMask.NameToLayer("Floor")))
			{
				Vector3 pos = hit.point;
				pos.y = .5f;
				SpawnResBox(pos);
			}
		}
	}

	// Update is called once per frame
	void Update()
    {
		ListenForCitizenSpawnInput();
		ListenForBoxSpawnInput();

		TickFinancialPeriodTimer();
		UpdateBoxPriceMetrics();

		if (_enableResBoxRespawning)
		{
			UpdateBoxRespawnClock();
		}

		RefreshTotalMoneyLabel();
		RefreshAverages();
	}
}
