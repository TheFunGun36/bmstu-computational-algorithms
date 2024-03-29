using Godot;
using System;
using System.Collections.Generic;

public class Table : Control
{
	private VBoxContainer _rowsParent;
	private List<Row> _rows = new List<Row>();
	private PackedScene _rowScene = GD.Load<PackedScene>("res://TableRow.tscn");
	private Plotter _plotter;
	private LineEdit _linePoly;
	private Label _headZ;
	private SpinBox _inputY;
	private OptionButton _inputMode;
	private bool _is3D = true;

	public double[] Coef = new double[3];
	public int Basis { get => Coef.Length; set => Coef = new double[value]; }
	public int Power { get => Coef.Length - 1; set => Coef = new double[value + 1]; }

	[Export] public NodePath RowsParentPath { get; set; }
	[Export] public NodePath PlotterPath { get; set; }
	[Export] public NodePath LinePolyPath { get; set; }
	[Export] public NodePath HeadZPath { get; set; }
	[Export] public NodePath InputYPath { get; set; }
	[Export] public NodePath InputModePath { get; set; }

	public override void _Ready()
	{
		_rowsParent = GetNode<VBoxContainer>(RowsParentPath);
		_plotter = GetNode<Plotter>(PlotterPath);
		_linePoly = GetNode<LineEdit>(LinePolyPath);
		_plotter.Function = ApproxValue;
		_headZ = GetNode<Label>(HeadZPath);
		_inputY = GetNode<SpinBox>(InputYPath);
		_inputMode = GetNode<OptionButton>(InputModePath);

		AddRow(-1.82, 3.11, 0, 1);
		AddRow(-1.25, -0.34, 0, 1);
		AddRow(0.17, -1.21, 0, 1);
		AddRow(0.79, -0.39, 0, 1);
		AddRow(2.22, 3.11, 0, 1);

		CompileFunction();
	}

	public void AddRow() => AddRow(0.0, 0.0);
	public void AddRow(double x, double y, double z = 0.0, double weight = 1.0)
	{
		Row row = _rowScene.Instance<Row>();
		row._Ready();
		row.Name = "Row" + _rows.Count;
		row.Index = _rows.Count;
		row.SetZVisible(_is3D);
		row.X = x;
		row.Y = y;
		row.Z = z;
		row.Weight = weight;
		row.Connect("Changed", this, "UpdatePoint");

		_rowsParent.AddChild(row);
		_rows.Add(row);
		_plotter.Points.Add(new Vector2((float)x, (float)y));
		_plotter.Update();
	}
	public void RemoveRow()
	{
		if (_rows.Count > 1)
		{
			int last = _rows.Count - 1;
			_rows[last].QueueFree();
			_rows.RemoveAt(last);
			_plotter.Points.RemoveAt(last);
			CompileFunction();
			_plotter.Update();
		}
	}
	public void Clear()
	{
		while (_rows.Count > Basis)
			RemoveRow();
	}

	public void UpdatePoint(int index)
	{
		Row row = _rows[index];
		_plotter.Points[index] = new Vector2((float)row.X, (float)row.Y);
		CompileFunction();
		_plotter.Update();
	}

	public double ApproxValue(double param)
	{
		double sum = 0.0;
		double mul = 1.0;

		for (int i = 0; i < Basis; i++)
		{
			sum += mul * Coef[i];
			mul *= param;
		}

		return sum;
	}
	private double[,] MakeSystem()
	{
		double[,] matrix = new double[Basis, Basis + 1];

		for (int i = 0; i < Basis; i++)
		{
			double sum;

			for (int j = 0; j < Basis; j++)
			{
				sum = 0.0;
				for (int k = 0; k < _rows.Count; k++)
					sum += _rows[k].Weight * Math.Pow(_rows[k].X, i) * Math.Pow(_rows[k].X, j);
				matrix[i, j] = sum;
			}

			sum = 0.0;
			for (int k = 0; k < _rows.Count; k++)
				sum += _rows[k].Weight * _rows[k].Y * Math.Pow(_rows[k].X, i);
			matrix[i, Basis] = sum;
		}
		
		return matrix;
	}
	public double[] Gauss(double[,] A, int n)
	{
		for (int i = 0; i < n; i++)
		{
			for (int j = i; j < n; j++)
			{
				double T = A[j, i];
				for (int k = 0; k < n + 1; k++)
					A[j, k] /= T;
			}

			for (int j = i + 1; j < n; j++)
				for (int k = i; k < n + 1; k++)
					A[j, k] -= A[i, k];
		}

		for (int i = n - 1; i > 0; i--)
		{
			for (int j = 0; j < i; j++)
			{
				double T = A[j, i];
				for (int k = i; k < n + 1; k++)
					A[j, k] -= A[i, k] * T;
			}
		}

		double[] C = new double[n];
		for (int i = 0; i < n; i++)
			C[i] = A[i, n];

		return C;
	}

	private double[,] FormTable()
	{
		double[,] matrix = new double[2, _rows.Count];

		for (int i = 0; i < _rows.Count; i++)
		{
			matrix[0, i] = _rows[i].X;
			matrix[1, i] = _rows[i].Y;
		}

		return matrix;
	}
	public void CompileFunction()
	{
		double[,] matrix = MakeSystem();
		double[] result = Gauss(matrix, Basis);
		for (int i = 0; i < Basis; i++)
			Coef[i] = matrix[i, Basis];
		_plotter.Replot();
		_linePoly.Text = PolyToString();
	}
	private string PolyToString()
	{
		string result = "";
		for (int i = 0; i < Coef.Length; i++)
		{
			result += String.Format("{0:0.00}", Coef[i]);
			if (i != 0)
				result += "x^" + i.ToString();
			if (i < Coef.Length - 1)
				result += " + ";
		}
		return result;
	}
	
	private double DetermineMul(double x, double y, int index)
	{
		double res = 1.0;
		switch (index) {
			case 1:
				res = x;
				break;
			case 2:
				res = y;
				break;
			case 3:
				res = x * x;
				break;
			case 4:
				res = x * y;
				break;
			case 5:
				res = y * y;
				break;
		}
		return res;
	}
	
	public double PlaneFunction(double x, double y)
	{
		double[,] matrix = new double[3, 4];
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				matrix[i, j] = 0.0;
				for (int k = 0; k < _rows.Count; k++) {
					double a = DetermineMul(_rows[k].X, _rows[k].Y, i);
					double b = DetermineMul(_rows[k].X, _rows[k].Y, j);
						
					matrix[i, j] += _rows[k].Weight * a * b;
				}
			}
			
			matrix[i, 3] = 0.0;
			for (int k = 0; k < 3; k++) {
				double a = 1.0;
				
				if (i == 1)
					a = _rows[k].X;
				else if (i == 2)
					a = _rows[k].Y;
					
				matrix[i, 3] += _rows[k].Weight * _rows[k].Z * a;
			}
		}
		
		double[] c = Gauss(matrix, 3);
		
		return c[0] + c[1] * x + c[2] * y;
	}
	
	public double ParaboloidFunction(double x, double y)
	{
		double[,] matrix = new double[6, 7];
		for (int i = 0; i < 6; i++)
		{
			for (int j = 0; j < 6; j++)
			{
				matrix[i, j] = 0.0;
				for (int k = 0; k < _rows.Count; k++)
				{
					double a = DetermineMul(_rows[k].X, _rows[k].Y, i);
					double b = DetermineMul(_rows[k].X, _rows[k].Y, j);
					
					matrix[i, j] += _rows[k].Weight * a * b;
				}
			}
			
			matrix[i, 6] = 0.0;
			for (int k = 0; k < _rows.Count; k++) {
				double a;
				a = DetermineMul(_rows[k].X, _rows[k].Y, i);
				
				matrix[i, 6] += _rows[k].Weight * _rows[k].Z * a;
			}
		}
		
		//for (int i = 0; i < 6; i++) {
		//	GD.Print(String.Format("{0:0.00} {1:0.00} {2:0.00} {3:0.00} {4:0.00} {5:0.00} {6:0.00}",
		//	matrix[i, 0], matrix[i, 1], matrix[i, 2], matrix[i, 3], matrix[i, 4], matrix[i, 5], matrix[i, 6]));
		//}
		
		double[] c = Gauss(matrix, 6);
		
		return c[0] + c[1] * x + c[2] * y + c[3] * x * x + c[4] * x * y + c[5] * y * y;
	}
	
	private void OnPowerChange(float value)
	{
		Power = (int)value;
		if (Basis > _rows.Count)
			AddRow();
		CompileFunction();
	}
	private void Toggle3D(bool value)
	{
		_is3D = value;
		UpdateVisibility();
	}
	private void UpdateVisibility()
	{
		_headZ.Visible = _is3D;
		_inputY.Visible = _is3D;
		_inputMode.Visible = _is3D;
		for (int i = 0; i < _rows.Count; i++)
			_rows[i].SetZVisible(_is3D);
	}
}
