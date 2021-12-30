using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GraphChart : MonoBehaviour
{
	[SerializeField] private TMP_Text _titleLabel;
	[SerializeField] private string _title;

	[Space]
	[SerializeField] private GameObject _pointPrefab;
	[SerializeField] private GameObject _pointContainer;

	[Header("Lines")]
	[SerializeField] private List<GameObject> _xLines;
	[SerializeField, ReadOnly] private int _xLineShifts = 0;
	[SerializeField] private float _xLineSpacing = 100;
	[SerializeField] private float _xLineOffset = 50;
	[SerializeField] private List<GameObject> _yLines;
	[SerializeField, ReadOnly] private int _yLineShifts = 0;
	[SerializeField] private float _yLineSpacing = 50;
	[SerializeField] private float _yLineOffset = 0;

	private Queue<GameObject> _points;

	public void AddPoint(float x, float y)
	{
		var go = Instantiate(_pointPrefab, _pointContainer.transform);
		go.transform.localPosition = new Vector3(x, y, 0);
		_points.Enqueue(go);
	}

	public void AddValue(float value)
	{
		ShiftPointsLeft();
		AddPoint(_xLineOffset + (_xLines.Count - 1) * _xLineSpacing, value);
	}

	// Start is called before the first frame update
	void Start()
    {
		ClearPoints();

		_titleLabel.text = _title;
    }

	void ShiftPointsLeft()
	{
		if (_points.Count > 0)
		{
			Destroy(_points.Dequeue());
		}

		foreach (GameObject point in _points)
		{
			point.transform.localPosition -= Vector3.left * _xLineSpacing;
		}

		for (int i = 0; i < _xLines.Count; i++)
		{
			TMP_Text lineLabel = _xLines[i].GetComponentInChildren<TMP_Text>();
			lineLabel.text = $"{i * _xLineShifts}";
		}
	}

	void ClearPoints()
	{
		for (int i = 0; i < _pointContainer.transform.childCount; i++)
		{
			Destroy(_pointContainer.transform.GetChild(i).gameObject);
		}

		_points = new Queue<GameObject>();
	}
}
