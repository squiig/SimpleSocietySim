using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BoxVendor : MonoBehaviour
{
	[SerializeField] private TMP_Text _nameLabel;

	[SerializeField, ReadOnly] private bool _isIdling; // just for aesthetic effect, doesn't cost money

	[SerializeField] private float _speed = 5f;
	[SerializeField] private float _fieldRadius = 25f;
	[SerializeField] private float _idlingRadius = .5f;

	private Vector3 _origin;
	private Vector3 _target;
	private Vector3 _anchor;

	GameManager _gameManager;

	private string _name;

	public bool IsFrozen { get; set; }
	public string Name => _name;

	private void Awake()
	{
		_gameManager = FindObjectOfType<GameManager>();

		_origin = transform.position;
		_target = _origin;
		_anchor = _origin;
	}

	private void Start()
	{
		SetIdling(true);

		_name = GetRandomName();
		_nameLabel.text = _name;
	}

	private void Update()
	{
		if (IsFrozen)
			return;

		// Idling (no cost)
		if (_isIdling && Random.value < .001f)
		{
			_target.x = Mathf.Clamp(_anchor.x + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
			_target.z = Mathf.Clamp(_anchor.z + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
		}

		// Traveling
		float targetDist = Vector3.Distance(transform.position, _target);

		bool destinationReached = targetDist <= .001f;
		if (!destinationReached)
		{
			Move();
		}
	}

	/// <summary>
	/// Gives a citizen reward money for his boxes. Unit price always equals current stock price.
	/// </summary>
	/// <param name="giver"></param>
	/// <param name="amount"></param>
	/// <returns>The amount of cash rewarded.</returns>
	public float GiveBoxes(Citizen giver, int amount)
	{
		if (giver.TotalResBoxesOwned < amount)
			return 0f;

		float reward = amount * _gameManager.CurrentAverageResBoxTradingPrice;
		if (reward == 0f)
		{
			reward = amount * _gameManager.CurrentAverageResBoxValuation;
		}

		giver.AddMoney(reward);
		giver.AddResBoxes(-amount);
		return reward;
	}

	string GetRandomName()
	{
		TextAsset mytxtData = (TextAsset)Resources.Load("names");
		string txt = mytxtData.text;
		string[] names = txt.Split('\n');
		return names[Random.Range(0, names.Length - 1)];
	}

	void SetIdling(bool idle)
	{
		if (idle)
		{
			Halt();

			_isIdling = true;
		}
		else
		{
			_isIdling = false;
		}
	}

	public void ReleaseAttention()
	{
		IsFrozen = false;
		SetIdling(true);
	}

	public void Halt()
	{
		_anchor = transform.position;
		_target = _anchor;
	}

	void Move()
	{
		Vector3 nextStep = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);

		nextStep.y = _origin.y;
		transform.position = nextStep;
	}
}
