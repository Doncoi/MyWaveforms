﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;
using MyWaveForms.Entity;
using MyWaveForms.Controller;
using ScottPlot.Plottable;
using ScottPlot;
using MyWaveForms.Test;
using MyWaveForms.Generator;
using System.Diagnostics;

namespace MyWaveForms
{
	public partial class Form1 : Form
	{
		//跨线程操作会引起异常，接收数据是新开线程，窗口的接受文本在主线程
		//两个解决方案任选其一
		//解决方案一：新建委托来显示接受内容
		//protected delegate void UpdateDisplayDelegate(string text, TextBox textBox);

		#region 全局变量
		/*
		公用
		*/
		private Boolean isInit = false;                         //IndexChange事件屏蔽标记
		private FormsPlotController formsPlotController;        //图表控制器
		WaveformDataGenerator waveformDataGenerator;            //数据生成器
		ChartTester tester;                                     //图表测试器                                             
		SerialPortController serialPortController;              //串口助手控制器
		/*
		示波器部分
		*/
		Stopwatch stopwatch;
		private SignalPlotXY signalPlotXYScope;                 //信号图对象
		private Crosshair crosshair;                            //鼠标随动十字光标线
		/*
		信号发生器部分
		*/                                                    
		const int PlotSize = 1000;                              //预览图表点数
		private WavegenChartController wavegenChartController;  //示波器图表控制器
		private SignalPlotXY signalPlotXYWaveGen;               //信号图对象
		/*
		电压电流计
		*/
		private TracerController meterTracerController;
		private SignalPlotXY signalPlotXYMeter;                 //信号图对象
		/*
		频谱分析仪
		*/
		private TracerController spectrumTracerController;//Tracer控制器
		private SignalPlotXY signalPlotXYSpectrum;                 //信号图对象
		#endregion

		public Form1()
		{
			this.Width = 1760;
			this.Height = 1200;
			InitializeComponent();
			//解决方案二：取消跨线程检查
			//Control.CheckForIllegalCrossThreadCalls = false;
			/*
			公用
			*/
			serialPortController = new SerialPortController();
			formsPlotController = new FormsPlotController();
			waveformDataGenerator = new WaveformDataGenerator();
			/*
			示波器部分
			*/
			stopwatch = new Stopwatch();
			tester = new ChartTester(this.formsPlotScope);
			signalPlotXYScope = new SignalPlotXY();
			/*
			信号发生器部分
			*/
			wavegenChartController = new WavegenChartController();
			signalPlotXYWaveGen = new SignalPlotXY();
			/*
			电压电流计
			*/
			meterTracerController = new TracerController(panelMeter, formsPlotMeter, 0, 6, 198, 100, 0, 0);
			signalPlotXYMeter = new SignalPlotXY();
			/*
			频谱分析仪
			*/
			spectrumTracerController = new TracerController(panelSpectrum, formsPlotSpectrum, 1, 10, 198, 100, 0, 260 + 10);
			signalPlotXYSpectrum = new SignalPlotXY();
		}

		#region 串口助手部分
		private void InitUARTAsssist()
		{
			//serialPort1 = new SerialPort();
			//serialPort1.Encoding = Encoding.GetEncoding("GBK2312");    //串口编码引入GBK2312
			serialPortController.ReflashPortToComboBox(serialPort1, comboBoxPort);    //启动时扫描可用串口

			buttonClosePort.Enabled = false;    //禁用关闭串口按钮
			buttonSend.Enabled = false;     //禁用发送按钮
			comboBoxBaudRate.SelectedIndex = 3;     //波特率默认设置
			comboBoxDataBit.SelectedIndex = 1;     //数据位默认设置
			comboBoxCheckBit.SelectedIndex = 0;     //校验位默认设置
			comboBoxStopBit.SelectedIndex = 0;     //停止位默认设置
		}

		private void tabPageAUARTAssist_Enter(object sender, EventArgs e)
		{
			serialPortController = new SerialPortController(); 
			InitUARTAsssist();
		}
		#region 控件禁用/启用
		//禁用串口设置的相关ComboBox
		private void DisablePortSettingComboBox()
		{
			comboBoxBaudRate.Enabled = false;
			comboBoxPort.Enabled = false;
			comboBoxDataBit.Enabled = false;
			comboBoxCheckBit.Enabled = false;
			comboBoxStopBit.Enabled = false;
		}

		//启用串口设置的相关ComboBox
		private void EnablePortSettingComboBox()
		{
			comboBoxBaudRate.Enabled = true;
			comboBoxPort.Enabled = true;
			comboBoxDataBit.Enabled = true;
			comboBoxCheckBit.Enabled = true;
			comboBoxStopBit.Enabled = true;
		}

		//串口打开状态，禁用开启串口按钮
		private void DisableOpenPortButton()
		{
			buttonSend.Enabled = true;      //启用发送按钮
			buttonClosePort.Enabled = true;    //启用关闭串口按钮
			buttonOpenPort.Enabled = false;    //禁用开启串口按钮
		}

		//串口关闭状态，启用开启串口按钮
		private void EnableOpenPortButton()
		{
			buttonSend.Enabled = false;      //禁用发送按钮
			buttonClosePort.Enabled = false;    //禁用关闭串口按钮
			buttonOpenPort.Enabled = true;    //启用开启串口按钮
		}
		#endregion
		#region 串口设置
		//端口号lable点击事件，刷新端口
		private void labelSerialPortNumber_Click(object sender, EventArgs e)
		{
			serialPortController.ReflashPortToComboBox(serialPort1, comboBoxPort);
		}

		//打开串口按钮点击事件
		private void buttonOpenPort_Click(object sender, EventArgs e)
		{
			if (comboBoxPort.SelectedItem == null)
			{
				MessageBox.Show("没有选择串口！");
				return;
			}
			if (comboBoxBaudRate.SelectedItem == null)
			{
				MessageBox.Show("没有选择波特率！");
				return;
			}
			//获取ComboBox数据
			SerialPortInfor portConfig1 = new SerialPortInfor(
				comboBoxPort.SelectedItem.ToString(),
				comboBoxBaudRate.SelectedItem.ToString(),
				comboBoxDataBit.SelectedItem.ToString(),
				comboBoxCheckBit.SelectedItem.ToString(),
				comboBoxStopBit.SelectedItem.ToString());
			//打开串口
			serialPortController.OpenPort(serialPort1, portConfig1);
			DisablePortSettingComboBox();   //禁用串口设置的相关ComboBox
			DisableOpenPortButton();    //禁用开启串口按钮
		}

		//关闭串口按钮点击事件
		private void buttonClosePort_Click(object sender, EventArgs e)
		{
			serialPortController.ClosePort(serialPort1);
			EnablePortSettingComboBox();
			EnableOpenPortButton();
		}
		#endregion
		#region 接收/发送设置
		//清空接受窗按钮点击事件
		private void buttonClearReceived_Click(object sender, EventArgs e)
		{
			textBoxReceived.Clear();
		}

		//清空发送窗按钮点击事件
		private void buttonClearSent_Click(object sender, EventArgs e)
		{
			textBoxSend.Clear();
		}

		//屏蔽接收信息复选框状态变化时，丢弃接收缓冲区数据
		private void checkBoxReceivedBlock_CheckedChanged(object sender, EventArgs e)
		{
			serialPort1.DiscardInBuffer();      //丢弃接收缓冲区数据
		}
		#endregion
		#region 接收/发送
		//串口数据接收事件
		private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				//如果激活屏蔽接收，则不显示接收数据
				if (true == checkBoxReceivedBlock.Checked)
					return;
				//获取接收模式
				byte bCommuFormat;
				if (radioButtonReceiveASCII.Checked == true) bCommuFormat = 0;
				else if (radioButtonReceiveHEX.Checked == true) bCommuFormat = 1;
				else bCommuFormat = 2;
				//获取接收到的字符串
				string strReceivedContent = serialPortController.GetReceivedString(serialPort1,bCommuFormat);
				//添加时间戳并显示
				textBoxReceived.AppendText(serialPortController.AppendTimestamp(strReceivedContent, false, bCommuFormat));
				/*Invoke(new UpdateDisplayDelegate(UpdateDisplayToTexxBox),
						new object[] { strReceivedContent, textBoxReceived });*/
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message, "接收数据异常提示");
			}
		}

		//发送数据按钮点击事件
		private void buttonSend_Click(object sender, EventArgs e)
		{
			if (serialPort1.IsOpen)    //如果串口已经打开
			{
				if (textBoxSend.Text != "")    //如果发送窗有数据
				{
					try
					{
						//获取发送格式
						byte bCommuFormat;
						if (radioButtonSendASCII.Checked == true) bCommuFormat = 0;
						else bCommuFormat = 1;
						//获取发送区文本
						string strSendContent = textBoxSend.Text;    
						//串口发送
						serialPortController.SerialPortSend(serialPort1, strSendContent, bCommuFormat);
						//添加日志
						textBoxReceived.AppendText(serialPortController.AppendTimestamp(strSendContent, true, bCommuFormat));    
					}
					catch (System.Exception ex)
					{
						serialPort1.Close();    //关闭串口
						EnablePortSettingComboBox();
						EnableOpenPortButton();
						MessageBox.Show(ex.Message, "串口数据写入错误");
					}
				}
			}
		}
		#endregion
		#endregion

		#region 示波器部分
		#region 控件初始化
		//初始化控件
		private void InitScopeWidget()
		{
			if(formsPlotController == null)	//实例化图表控制器
				formsPlotController = new FormsPlotController();
			//if (scopeChartTester == null)   //实例化测试控制器
				//scopeChartTester = new ChartTester(formsPlotScope);

			isInit = true;      //打开屏蔽标记
			//显示区控件初始化
			toolStripComboBoxColorStyle.SelectedIndex = 0;    //浅色
			toolStripComboBoxLineStyle.SelectedIndex = 0;    //折线
			toolStripComboBoxLineWidth.SelectedIndex = 0;     //线宽：1
			//控制区控件初始化
			comboBoxCoupledType.SelectedIndex = 0;      //DC
			comboBoxTriggerType.SelectedIndex = 0;      //同时触发
			comboBoxVerticalSensitivity.SelectedIndex = 2;      //0.1V
			comboBoxTimeBase.SelectedIndex = 9;     //1ms
			//信息区初始化
			labelDisplayPlotCount.Text = "1000";
			labelDisplayFrequency.Text = "50MHz";
			isInit = false;     //关闭屏蔽标记

			//无数据初始化图表
			ValueTuple<SignalPlotXY, Crosshair> vt = formsPlotController.InitChart(formsPlotScope,
				toolStripComboBoxColorStyle.SelectedIndex,								//获取色彩模式
				Double.Parse(toolStripComboBoxLineWidth.SelectedItem.ToString()),		//获取线宽
				toolStripComboBoxLineStyle.SelectedIndex);								//获取连线样式
			this.signalPlotXYScope = vt.Item1;
			this.crosshair = vt.Item2;
		}

		private void tabPageScope_Enter(object sender, EventArgs e)
		{
			InitScopeWidget();
		}
		#endregion
		#region 示波器图表显示设置 已完成，不要动
		//变更图表色彩模式
		private void toolStripComboBoxColorStyle_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int colorStyle = toolStripComboBoxColorStyle.SelectedIndex;      //获取色彩模式
				formsPlotController.SetPlotColorStyle(this.formsPlotScope, this.signalPlotXYScope, colorStyle);     //变更色彩模式
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		// 变更图表线条样式
		private void toolStripComboBoxLineStyle_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int iLineStyle = toolStripComboBoxLineStyle.SelectedIndex;       //获取连线样式
				formsPlotController.SetLineStyle(this.formsPlotScope, this.signalPlotXYScope, iLineStyle);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		// 变更图表线条粗细
		private void toolStripComboBoxLineWidth_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				double dLineWidth = Double.Parse(toolStripComboBoxLineWidth.SelectedItem.ToString());    //获取线宽
				formsPlotController.SetLineWidth(this.formsPlotScope, this.signalPlotXYScope, dLineWidth);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		#endregion
		#region 示波器图表交互设置 已完成，不要动
		//鼠标右键拖拽缩放
		private void toolStripMenuItemRightClickDragZoom_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemRightClickDragZoom.Checked = 
				! this.toolStripMenuItemRightClickDragZoom.Checked;
			this.formsPlotScope.Configuration.RightClickDragZoom = 
				this.toolStripMenuItemRightClickDragZoom.Checked;
		}

		//鼠标滚轮缩放

		private void toolStripMenuItemScrollWheelZoom_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemScrollWheelZoom.Checked =
				! this.toolStripMenuItemScrollWheelZoom.Checked;
			this.formsPlotScope.Configuration.ScrollWheelZoom = 
				this.toolStripMenuItemScrollWheelZoom.Checked;
		}

		//启用/禁用水平缩放
		private void toolStripMenuItemHorizontalZoom_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemHorizontalZoom.Checked =
				! this.toolStripMenuItemHorizontalZoom.Checked;
			this.formsPlotScope.Configuration.LockHorizontalAxis = 
				! this.toolStripMenuItemHorizontalZoom.Checked;
		}

		//启用/禁用垂直缩放
		private void toolStripMenuItemVerticalZoom_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemVerticalZoom.Checked =
				! this.toolStripMenuItemVerticalZoom.Checked;
			this.formsPlotScope.Configuration.LockVerticalAxis = 
				! this.toolStripMenuItemVerticalZoom.Checked;
		}

		//启用/禁用十字准线
		private void toolStripMenuItemCrosshair_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemCrosshair.Checked =
				!this.toolStripMenuItemCrosshair.Checked;
			try
			{
				if (toolStripMenuItemCrosshair.Checked)
				{
					this.crosshair.VerticalLine.IsVisible = true;
					this.crosshair.HorizontalLine.IsVisible = true;
				}
				else
				{
					this.crosshair.VerticalLine.IsVisible = false;
					this.crosshair.HorizontalLine.IsVisible = false;
				}
				this.formsPlotScope.Refresh(true, true);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		//鼠标移动时处理十字光标
		private void formsPlotMain_MouseMove(object sender, MouseEventArgs e)
		{
			if (toolStripMenuItemCrosshair.Checked == false) return;      //如果不需要绘制十字准线，直接返回
			//获取鼠标位置
			ValueTuple<double, double> mouseCoordinates = this.formsPlotScope.GetMouseCoordinates();
			double coordinateX = mouseCoordinates.Item1;
			double coordinateY = mouseCoordinates.Item2;
			//移动十字准线
			this.crosshair.X = coordinateX;
			this.crosshair.Y = coordinateY;

			//RefreshPlot(formsPlotScope, true, true);
			this.formsPlotScope.Refresh(true, true);
		}

		//鼠标进入图表中执行的任务
		private void formsPlotMain_MouseEnter(object sender, EventArgs e)
		{
			try
			{
				if (toolStripMenuItemCrosshair.Checked == false) return;      //如果不需要绘制十字准线，直接返回
				if (toolStripMenuItemCrosshair.Checked)		//如果需要显示十字准线
				{
					//this.crosshair.IsVisible = true;
					this.crosshair.VerticalLine.IsVisible = true;	//设为可见
					this.crosshair.HorizontalLine.IsVisible = true;
				}
				/*
				else      //如果不需要显示十字准线
				{
					//this.crosshair.IsVisible = false;
					this.crosshair.VerticalLine.IsVisible = false;		//设为不可见
					this.crosshair.HorizontalLine.IsVisible = false;
				}
				*/

				this.formsPlotScope.Refresh();
			}
			catch(System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		//鼠标离开图表执行的任务
		private void formsPlotMain_MouseLeave(object sender, EventArgs e)
		{
			//Console.WriteLine(new StackTrace(new StackFrame(true)).GetFrame(0));
			try
			{
				//if (null == this.crosshair) return;
				this.crosshair.VerticalLine.IsVisible = false;
				this.crosshair.HorizontalLine.IsVisible = false;
				this.formsPlotScope.Refresh();
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		#endregion
		#region 信息区 to-do
		#endregion
		#endregion

		#region 信号发生器部分
		#region 控件初始化
		//初始化信号发生器页
		private void tabPageWavegen_Enter(object sender, EventArgs e)
		{
			//实例化图表控制器
			if(wavegenChartController == null)
				wavegenChartController = new WavegenChartController();
			/*
			//实例化图表测试器
			if (wavegenChartTester == null)
				wavegenChartTester = new ChartTester(formsPlotWaveGen);
			*/
			isInit = true;
			//显示区控件初始化
			toolStripComboBoxColorStyle2.SelectedIndex = 0;    //浅色
			toolStripComboBoxLineStyle2.SelectedIndex = 0;    //折线
			toolStripComboBoxLineWidth2.SelectedIndex = 0;     //线宽：1
			//初始化ComboBox默认项
			comboBoxFrequency.SelectedIndex = comboBoxFrequency.Items.IndexOf("1kHz");
			comboBoxAmplitde.SelectedIndex = comboBoxAmplitde.Items.IndexOf("2V");
			comboBoxOffset.SelectedIndex = comboBoxOffset.Items.IndexOf("0V");
			comboBoxSymmetry.SelectedIndex = comboBoxSymmetry.Items.IndexOf("50%");
			comboBoxPhase.SelectedIndex = comboBoxPhase.Items.IndexOf("0°");
			//进入界面时禁用所有选项
			EnableWidget(true, false, false, false, false, false);
			isInit = false;
			//设置标签
			formsPlotWaveGen.Plot.Title("预览");
			//设置坐标轴
			formsPlotWaveGen.Plot.XLabel("ms");
			formsPlotWaveGen.Plot.YLabel("V");
			formsPlotWaveGen.Plot.SetAxisLimitsX(0, 1);
		}

		//启用/禁用控件
		private void EnableWidget(bool bWaveType, bool bAmplitude, bool bFrequency, bool bOffset, bool bPhase, bool bSymmetry)
		{
			isInit = true;
			comboBoxWaveType.Enabled = bWaveType;	   //禁用波形选项
			comboBoxAmplitde.Enabled = bAmplitude;		//禁用幅值选项
			comboBoxFrequency.Enabled = bFrequency;     //禁用频率选项
			comboBoxOffset.Enabled = bOffset;     //禁用偏移选项
			comboBoxPhase.Enabled = bPhase;     //禁用相位选项
			comboBoxSymmetry.Enabled = bSymmetry;       //禁用对称选项
			isInit = false;
		}
		#endregion
		#region 发送波形/停止发送

		//运行/停止按钮
		private void buttonRunWaveGen_Click(object sender, EventArgs e)
		{
			if (checkBoxRunState.Checked == false)      //如果当前没有在运行
			{
				//to-do 产生波形
				//运行时禁用所有配置选择控件
				EnableWidget(false, false, false, false, false, false);
				//更改运行按钮样式
				//buttonRunWavegen.BackColor = Color.Tomato;
				buttonRunWavegen.Text = "停止";
				checkBoxRunState.Checked = true;
			}
			else
			{
				//to-do 接受波形
				//重新激活控件
				if (comboBoxWaveType.SelectedIndex == 0)        //对DC电源
					EnableWidget(true, true, false, false, false, false);
				else if (comboBoxWaveType.SelectedIndex == 6)     //对噪声选项
					EnableWidget(true, true, true, true, false, false);
				else
					EnableWidget(true, true, true, true, true, false);
				//buttonRunWavegen.BackColor = Color.LimeGreen;
				buttonRunWavegen.Text = "运行";
				checkBoxRunState.Checked = false;
			}
		}
		#endregion
		#region 配置变更 已完成，不要动
		//获取当前配置并打包为List
		private List<string> GetWavegenConfigSelectedList()
		{
			List<string> list = new List<string>();
			if (comboBoxWaveType.SelectedItem == null)
			{
				return null;
			}
			else
			{
				list.Add(comboBoxWaveType.SelectedItem.ToString());
				list.Add(comboBoxFrequency.SelectedItem.ToString());
				list.Add(comboBoxAmplitde.SelectedItem.ToString());
				list.Add(comboBoxOffset.SelectedItem.ToString());
				list.Add(comboBoxSymmetry.SelectedItem.ToString());
				list.Add(comboBoxPhase.SelectedItem.ToString());
			}
			return list;
		}

		//波形选项发生变更
		private void comboBoxWaveType_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isInit == true) return;
			if(comboBoxWaveType.SelectedIndex == 0)		//对DC电源
				EnableWidget(true, true, false, false, false, false);
			else if (comboBoxWaveType.SelectedIndex == 6)     //对噪声选项
				EnableWidget(true, true, true, true, false, false);
			else
				EnableWidget(true, true, true, true, true, false);

			//获取配置信息并添加预览图线
			signalPlotXYWaveGen = wavegenChartController.AddWaveforms(formsPlotWaveGen, new WaveformInfor(GetWavegenConfigSelectedList()));
		}

		//频率选项发生变更
		private void comboBoxFrequency_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isInit == true) return;
			//重设坐标轴
			//wavegenChartController.SetAxis(formsPlotWaveGen, new WaveformInfor(GetSelectedList()));
			//获取配置信息并添加预览图线
			signalPlotXYWaveGen = wavegenChartController.AddWaveforms(formsPlotWaveGen, new WaveformInfor(GetWavegenConfigSelectedList()));
		}

		//幅值选项发生变更
		private void comboBoxAmplitde_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isInit == true) return;
			//获取配置信息并添加预览图线
			signalPlotXYWaveGen = wavegenChartController.AddWaveforms(formsPlotWaveGen, new WaveformInfor(GetWavegenConfigSelectedList()));
		}

		//y轴偏移量选项发生变更
		private void comboBoxOffset_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isInit == true) return;
			//获取配置信息并添加预览图线
			signalPlotXYWaveGen = wavegenChartController.AddWaveforms(formsPlotWaveGen, new WaveformInfor(GetWavegenConfigSelectedList()));
		}

		//对称偏移量选项发生变更
		private void comboBoxSymmetry_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isInit == true) return;
			//获取配置信息并添加预览图线
			signalPlotXYWaveGen = wavegenChartController.AddWaveforms(formsPlotWaveGen, new WaveformInfor(GetWavegenConfigSelectedList()));
		}

		//相位选项发生变更
		private void comboBoxPhase_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (isInit == true) return;
			//获取配置信息并添加预览图线
			signalPlotXYWaveGen = wavegenChartController.AddWaveforms(formsPlotWaveGen, new WaveformInfor(GetWavegenConfigSelectedList()));
		}
		#endregion
		#region 信号发生器图表显示设置 已完成，不要动
		private void toolStripComboBoxColorStyle2_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int colorStyle = toolStripComboBoxColorStyle2.SelectedIndex;      //获取色彩模式
				formsPlotController.SetPlotColorStyle(this.formsPlotWaveGen, this.signalPlotXYWaveGen, colorStyle);     //变更色彩模式
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripComboBoxLineStyle2_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int iLineStyle = toolStripComboBoxLineStyle2.SelectedIndex;       //获取连线样式
				formsPlotController.SetLineStyle(this.formsPlotWaveGen, this.signalPlotXYWaveGen, iLineStyle);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripComboBoxLineWidth2_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				double dLineWidth = Double.Parse(toolStripComboBoxLineWidth2.SelectedItem.ToString());    //获取线宽
				formsPlotController.SetLineWidth(this.formsPlotWaveGen, this.signalPlotXYWaveGen, dLineWidth);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		#endregion
		#region 信号发生器图表交互设置 已完成，不要动
		//鼠标右键拖拽缩放
		private void toolStripMenuItemRightClickDragZoom2_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemRightClickDragZoom2.Checked =
				!this.toolStripMenuItemRightClickDragZoom2.Checked;
			this.formsPlotScope.Configuration.RightClickDragZoom =
				this.toolStripMenuItemRightClickDragZoom2.Checked;
		}

		//鼠标滚轮缩放

		private void toolStripMenuItemScrollWheelZoom2_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemScrollWheelZoom2.Checked =
				!this.toolStripMenuItemScrollWheelZoom2.Checked;
			this.formsPlotScope.Configuration.ScrollWheelZoom =
				this.toolStripMenuItemScrollWheelZoom2.Checked;
		}

		//启用/禁用水平缩放
		private void toolStripMenuItemHorizontalZoom2_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemHorizontalZoom2.Checked =
				!this.toolStripMenuItemHorizontalZoom2.Checked;
			this.formsPlotScope.Configuration.LockHorizontalAxis =
				!this.toolStripMenuItemHorizontalZoom2.Checked;
		}

		//启用/禁用垂直缩放
		private void toolStripMenuItemVerticalZoom2_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemVerticalZoom2.Checked =
				!this.toolStripMenuItemVerticalZoom2.Checked;
			this.formsPlotScope.Configuration.LockVerticalAxis =
				!this.toolStripMenuItemVerticalZoom2.Checked;
		}
		#endregion
		#endregion

		#region 电压电流计
		#region 初始化
		private void tabPageMeter_Enter(object sender, EventArgs e)
		{
			//显示区控件初始化
			toolStripComboBoxColorStyle3.SelectedIndex = 0;    //浅色
			toolStripComboBoxLineStyle3.SelectedIndex = 0;    //折线
			toolStripComboBoxLineWidth3.SelectedIndex = 0;     //线宽：1
		}
		#endregion
		//添加追踪器
		private void toolStripLabelAddTracer_Click(object sender, EventArgs e)
		{
			meterTracerController.AddTracer();
		}

		//清空追踪器
		private void toolStripLabel2_Click(object sender, EventArgs e)
		{
			meterTracerController.ClearTracer();
		}
		#region 电压电流计图表显示设置 已完成，不要动
		private void toolStripComboBoxColorStyle3_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int colorStyle = toolStripComboBoxColorStyle3.SelectedIndex;      //获取色彩模式
				formsPlotController.SetPlotColorStyle(this.formsPlotMeter, this.signalPlotXYMeter, colorStyle);     //变更色彩模式
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripComboBoxLineStyle3_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int iLineStyle = toolStripComboBoxLineStyle3.SelectedIndex;       //获取连线样式
				formsPlotController.SetLineStyle(this.formsPlotMeter, this.signalPlotXYMeter, iLineStyle);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripComboBoxLineWidth3_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				double dLineWidth = Double.Parse(toolStripComboBoxLineWidth3.SelectedItem.ToString());    //获取线宽
				formsPlotController.SetLineWidth(this.formsPlotMeter, this.signalPlotXYMeter, dLineWidth);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		#endregion
		#region 电压电流计图表交互设置 已完成，不要动
		//鼠标右键拖拽缩放
		private void toolStripMenuItemRightClickDragZoom3_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemRightClickDragZoom3.Checked =
				!this.toolStripMenuItemRightClickDragZoom3.Checked;
			this.formsPlotMeter.Configuration.RightClickDragZoom =
				this.toolStripMenuItemRightClickDragZoom3.Checked;
		}

		//鼠标滚轮缩放

		private void toolStripMenuItemScrollWheelZoom3_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemScrollWheelZoom3.Checked =
				!this.toolStripMenuItemScrollWheelZoom3.Checked;
			this.formsPlotMeter.Configuration.ScrollWheelZoom =
				this.toolStripMenuItemScrollWheelZoom3.Checked;
		}

		//启用/禁用水平缩放
		private void toolStripMenuItemHorizontalZoom3_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemHorizontalZoom3.Checked =
				!this.toolStripMenuItemHorizontalZoom3.Checked;
			this.formsPlotMeter.Configuration.LockHorizontalAxis =
				!this.toolStripMenuItemHorizontalZoom3.Checked;
		}

		//启用/禁用垂直缩放
		private void toolStripMenuItemVerticalZoom3_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemVerticalZoom3.Checked =
				!this.toolStripMenuItemVerticalZoom3.Checked;
			this.formsPlotMeter.Configuration.LockVerticalAxis =
				!this.toolStripMenuItemVerticalZoom3.Checked;
		}
		#endregion
		#endregion

		#region 频谱分析仪
		#region 初始化
		private void tabPageSpectrum_Enter(object sender, EventArgs e)
		{
			//打开初始化标记
			isInit = true;
			//显示区控件初始化
			toolStripComboBoxColorStyle4.SelectedIndex = 0;    //浅色
			toolStripComboBoxLineStyle4.SelectedIndex = 0;    //折线
			toolStripComboBoxLineWidth4.SelectedIndex = 0;     //线宽：1
			//频谱设置区控件初始化
			toolStripComboBoxStart.SelectedIndex = 21;		//Start = 0Hz
			toolStripComboBoxStop.SelectedIndex = 2;	//Stop = 1MHz
			toolStripComboBoxBoxCount.SelectedIndex = 0;	//BoxCount = Default
			toolStripComboBoxScale.SelectedIndex = 0;	//Scale = Linear
			toolStripComboBoxAlgorithm.SelectedIndex = 0;   //Algorithm = FFT
			//振幅设置区控件初始化
			comboBoxMagnitudeUnit.SelectedIndex = 0;    //Unit = Vpeak(V)
			comboBoxTop.SelectedIndex = 8;      //Top = 20dBV
			comboBoxRange.SelectedIndex = 0;        //Range = 200dBV
			comboBoxReference.SelectedIndex = 13;       //Reference = 0.05V
			//信道设置区控件初始化
			comboBoxChannelOffset.SelectedIndex = 14;   //Offset = 0V
			comboBoxChannelRange.SelectedIndex = 0;     //Range = Auto
			comboBoxChannelAttenuation.SelectedIndex = 2;	 //Attenuation = 1X
			//关闭初始化标记
			isInit = false;
		}
		#endregion

		//添加追踪器
		private void buttonAddTrace_Click(object sender, EventArgs e)
		{
			spectrumTracerController.AddTracer();
		}

		#region 频谱分析仪表显示设置 已完成，不要动
		private void toolStripComboBoxColorStyle4_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int colorStyle = toolStripComboBoxColorStyle4.SelectedIndex;      //获取色彩模式
				formsPlotController.SetPlotColorStyle(this.formsPlotSpectrum, this.signalPlotXYSpectrum, colorStyle);     //变更色彩模式
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripComboBoxLineStyle4_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				int iLineStyle = toolStripComboBoxLineStyle4.SelectedIndex;       //获取连线样式
				formsPlotController.SetLineStyle(this.formsPlotSpectrum, this.signalPlotXYSpectrum, iLineStyle);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripComboBoxLineWidth4_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				if (isInit) return;     //如果正在初始化,则屏蔽更改事件
				double dLineWidth = Double.Parse(toolStripComboBoxLineWidth4.SelectedItem.ToString());    //获取线宽
				formsPlotController.SetLineWidth(this.formsPlotSpectrum, this.signalPlotXYSpectrum, dLineWidth);
			}
			catch (System.Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}
		#endregion
		#region 频谱分析仪图表交互设置 已完成，不要动
		//鼠标右键拖拽缩放
		private void toolStripMenuItemRightClickDragZoom4_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemRightClickDragZoom4.Checked =
				!this.toolStripMenuItemRightClickDragZoom4.Checked;
			this.formsPlotSpectrum.Configuration.RightClickDragZoom =
				this.toolStripMenuItemRightClickDragZoom4.Checked;
		}

		//鼠标滚轮缩放

		private void toolStripMenuItemScrollWheelZoom4_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemScrollWheelZoom4.Checked =
				!this.toolStripMenuItemScrollWheelZoom4.Checked;
			this.formsPlotSpectrum.Configuration.ScrollWheelZoom =
				this.toolStripMenuItemScrollWheelZoom4.Checked;
		}

		//启用/禁用水平缩放
		private void toolStripMenuItemHorizontalZoom4_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemHorizontalZoom4.Checked =
				!this.toolStripMenuItemHorizontalZoom4.Checked;
			this.formsPlotSpectrum.Configuration.LockHorizontalAxis =
				!this.toolStripMenuItemHorizontalZoom4.Checked;
		}

		//启用/禁用垂直缩放
		private void toolStripMenuItemVerticalZoom4_Click(object sender, EventArgs e)
		{
			this.toolStripMenuItemVerticalZoom4.Checked =
				!this.toolStripMenuItemVerticalZoom4.Checked;
			this.formsPlotSpectrum.Configuration.LockVerticalAxis =
				!this.toolStripMenuItemVerticalZoom4.Checked;
		}
		#endregion
		#endregion

		#region 测试页
		private void button6_Click(object sender, EventArgs e)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < spectrumTracerController.TracerList.Count; ++i)
			{
				stringBuilder.Append(i.ToString() + spectrumTracerController.TracerList[i].ToString() + Environment.NewLine);
			}
			textBox3.Text = stringBuilder.ToString();
		}


		#endregion

		Timer timerStopWatch = new Timer();
		double runTimeMS = 0;

		private void labelDemo_Click(object sender, EventArgs e)
		{
			if(this.labelDemo.BackColor == Color.Tomato)
			{
				#region 利用stopwatch更新运行时间
				stopwatch.Restart();
				timerStopWatch.Interval = 10;
				timerStopWatch.Tick += new System.EventHandler(UpdateRuntime);
				timerStopWatch.Start();
				#endregion
				//生成数据
				double[] dYValues = waveformDataGenerator.GenerateTrapeziumData(1000, 
					new WaveformInfor()).DValues;
				double[] dXValues = new double[dYValues.Length];
				for (int i = 1; i < dYValues.Length; i++) dXValues[i] = dXValues[i - 1] + 0.01;
				//初始化定时器
				tester.InitTimer();
				//初始化定时器数据
				tester.InitTestData(new ValueTuple<double[], double[]>(dXValues, dYValues));
				ValueTuple < SignalPlotXY, Crosshair > vt = formsPlotController.InitChart(formsPlotScope, 
					tester.GetTestData());
				this.signalPlotXYScope = vt.Item1;
				this.crosshair = vt.Item2;
				//开始计时器
				tester.StartTimer();
				this.labelDemo.BackColor = Color.LimeGreen;
			}
			else
			{
				stopwatch.Stop();
				timerStopWatch.Stop();
				tester.StopTimer();
				this.labelDemo.BackColor = Color.Tomato;
			}
		}

		public void UpdateRuntime(Object sender, EventArgs myEventArgs)
		{
			labelRunTime.Text = stopwatch.Elapsed.ToString();
		}
	}
}
