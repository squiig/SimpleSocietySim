using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BoxVendor : MonoBehaviour
{
	[SerializeField] private TMP_Text _nameLabel;

	[SerializeField, ReadOnly] private bool _isIdling; // just for aesthetic effect, doesn't cost money

	[SerializeField] private float _speed = 5f;
	[SerializeField] private float _rotSpeed = 1f;
	[SerializeField] private float _fieldRadius = 25f;
	[SerializeField] private float _idlingRadius = .5f;

	private Vector3 _originPos;
	private Vector3 _targetPos;
	private Vector3 _anchorPos;
	private Quaternion _prevRot;
	private Quaternion _targetRot;
	private float _rotTimer;

	GameManager _gameManager;

	private string _name;

	public bool IsFrozen { get; set; }
	public string Name => _name;

	private void Awake()
	{
		_gameManager = FindObjectOfType<GameManager>();

		_originPos = transform.position;
		_targetPos = _originPos;
		_anchorPos = _originPos;
		_targetRot = transform.rotation;
		_prevRot = transform.rotation;
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
			float x = Mathf.Clamp(_anchorPos.x + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
			float z = Mathf.Clamp(_anchorPos.z + Random.Range(-1f, 1f) * _idlingRadius, -_fieldRadius, _fieldRadius);
			SetDestination(x, z);
		}

		// Traveling
		float targetDist = Vector3.Distance(transform.position, _targetPos);

		bool destinationReached = targetDist <= .001f;
		if (!destinationReached)
		{
			Move();
		}

		Rotate();
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
		_anchorPos = transform.position;
		_targetPos = _anchorPos;
	}

	void SetDestination(Vector3 pos)
	{
		_targetPos = pos;
		Vector3 dir = _targetPos - transform.position;
		_prevRot = _targetRot;
		_targetRot = Quaternion.LookRotation(dir);
		_rotTimer = 0f;
	}

	void SetDestination(float x, float z)
	{
		SetDestination(new Vector3(x, _targetPos.y, z));
	}

	void Move()
	{
		Vector3 nextStep = Vector3.MoveTowards(transform.position, _targetPos, _speed * Time.deltaTime);

		nextStep.y = _originPos.y;
		Vector3 dir = nextStep - transform.position;
		transform.position = nextStep;
		transform.rotation = Quaternion.LookRotation(dir);
	}

	void Rotate()
	{
		if (Quaternion.Dot(transform.rotation, _targetRot) > .99f)
			return;

		if (_rotTimer < 1)
		{
			_rotTimer += Time.deltaTime * _rotSpeed;
		}

		Quaternion rot = Quaternion.Slerp(_prevRot, _targetRot, _rotTimer);
		transform.rotation = rot;
	}
}
