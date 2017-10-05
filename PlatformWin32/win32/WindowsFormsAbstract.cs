//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using MatterHackers.Agg.Platform;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class WinformsSystemWindowProvider : ISystemWindowProvider
	{
		private static IPlatformWindow platformWindow = null;

		/// <summary>
		/// Creates or connects a PlatformWindow to the given SystemWindow
		/// </summary>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			bool singleWindowMode = WinformsSystemWindow.SingleWindowMode;
			bool isFirstWindow = platformWindow == null;
			if ((singleWindowMode && platformWindow == null)
				|| !singleWindowMode)
			{
				platformWindow = AggContext.CreateInstanceFrom<IPlatformWindow>(AggContext.Config.ProviderTypes.SystemWindow);
			}

			if ((singleWindowMode && isFirstWindow)
				|| !singleWindowMode)
			{
				platformWindow.Caption = systemWindow.Title;
				platformWindow.MinimumSize = systemWindow.MinimumSize;
			}

			systemWindow.PlatformWindow = platformWindow;
			platformWindow.ShowSystemWindow(systemWindow);
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			platformWindow.CloseSystemWindow(systemWindow);
		}
	}

	public abstract class WinformsSystemWindow : Form, IPlatformWindow
	{
		private static Stack<SystemWindow> allOpenSystemWindows = new Stack<SystemWindow>();

		public static bool SingleWindowMode { get; set; } = false;

		public bool IsInitialized { get; set; } = false;

		public static bool EnableInputHook = true;

		private static System.Timers.Timer idleCallBackTimer = null;

		private static bool processingOnIdle = false;

		private static object singleInvokeLock = new object();

		protected WinformsEventSink EventSink;

		private SystemWindow systemWindow;
		public SystemWindow AggSystemWindow
		{
			get
			{
				return systemWindow;
			}
			set
			{
				systemWindow = value;

				if (systemWindow != null)
				{
					this.Caption = systemWindow.Title;

					if (SingleWindowMode)
					{
						// Set this system window as the event target
						this.EventSink?.SetActiveSystemWindow(systemWindow);
					}
					else
					{
						this.MinimumSize = systemWindow.MinimumSize;
					}
				}
			}
		}

		public bool IsMainWindow { get; } = false;

		public WinformsSystemWindow()
		{
			if (idleCallBackTimer == null)
			{
				idleCallBackTimer = new System.Timers.Timer();
				// call up to 100 times a second
				idleCallBackTimer.Interval = 10;
				idleCallBackTimer.Elapsed += InvokePendingOnIdleActions;
				idleCallBackTimer.Start();
			}

			// Track first window
			if (MainWindowsFormsWindow == null)
			{
				MainWindowsFormsWindow = this;
				IsMainWindow = true;
			}

			this.TitleBarHeight = RectangleToScreen(ClientRectangle).Top - this.Top;
			this.AllowDrop = true;

			string iconPath = File.Exists("application.ico") ?
				"application.ico" :
				"../MonoBundle/StaticData/application.ico";

			try
			{
				this.Icon = new Icon(iconPath);
			}
			catch { }
		}

		protected override void OnClosed(EventArgs e)
		{
			if (IsMainWindow)
			{
				// Ensure that when the MainWindow is closed, we null the field so we can recreate the MainWindow
				MainWindowsFormsWindow = null;
			}

			AggSystemWindow = null;

			base.OnClosed(e);
		}

		public void ReleaseOnIdleGuard()
		{
			lock (singleInvokeLock)
			{
				processingOnIdle = false;
			}
		}

		private void InvokePendingOnIdleActions(object sender, ElapsedEventArgs e)
		{
			if (AggSystemWindow?.HasBeenClosed == false)
			{
				lock (singleInvokeLock)
				{
					if (processingOnIdle)
					{
						// If the pending invoke has not completed, skip the timer event
						return;
					}

					processingOnIdle = true;
				}

				if (InvokeRequired)
				{
					Invoke(new Action(() =>
					{
						try
						{
							UiThread.InvokePendingActions();
						}
						catch (Exception invokeException)
						{
#if DEBUG
							lock (singleInvokeLock)
							{
								processingOnIdle = false;
							}

							throw (invokeException);
#endif
						}

						lock (singleInvokeLock)
						{
							processingOnIdle = false;
						}
					}));
				}
				else
				{
					UiThread.InvokePendingActions();
					processingOnIdle = false;
				}
			}
		}

		public static bool ShowingSystemDialog = false;

		public abstract Graphics2D NewGraphics2D();

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			if (AggSystemWindow == null
				|| AggSystemWindow.HasBeenClosed)
			{
				return;
			}

			base.OnPaint(paintEventArgs);

			if (ShowingSystemDialog)
			{
				// We do this because calling Invalidate within an OnPaint message will cause our
				// SaveDialog to not show its 'overwrite' dialog if needed.
				// We use the Invalidate to cause a continuous pump of the OnPaint message to call our OnIdle.
				// We could figure another solution but it must be very carful to ensure we don't break SaveDialog
				return;
			}

			Rectangle rect = paintEventArgs.ClipRectangle;
			if (ClientSize.Width > 0 && ClientSize.Height > 0)
			{
				DrawCount++;

				AggSystemWindow.OnDraw(this.NewGraphics2D());

				/*
				var bitmap = new Bitmap((int)SystemWindow.Width, (int)SystemWindow.Height);
				paintEventArgs.Graphics.DrawImage(bitmap, 0, 0);
				bitmap.Save($"c:\\temp\\gah-{DateTime.Now.Ticks}.png");
				*/
				CopyBackBufferToScreen(paintEventArgs.Graphics);
			}

			OnPaintCount++;
			// use this to debug that windows are drawing and updating.
			//Text = string.Format("Draw {0}, Idle {1}, OnPaint {2}", DrawCount, IdleCount, OnPaintCount);
		}

		private int DrawCount = 0;
		private int OnPaintCount;

		public abstract void CopyBackBufferToScreen(Graphics displayGraphics);

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			// don't call this so that windows will not erase the background.
			//base.OnPaintBackground(e);
		}

		protected override void OnActivated(EventArgs e)
		{
			// focus the first child of the forms window (should be the system window)
			if (AggSystemWindow != null
				&& AggSystemWindow.Children.Count > 0
				&& AggSystemWindow.Children[0] != null)
			{
				AggSystemWindow.Children[0].Focus();
			}

			base.OnActivated(e);
		}

		protected override void OnResize(EventArgs e)
		{
			AggSystemWindow.LocalBounds = new RectangleDouble(0, 0, ClientSize.Width, ClientSize.Height);

			// Wait until the control is initialized (and thus WindowState has been set) to ensure we don't wipe out
			// the persisted data before its loaded
			if (this.IsInitialized)
			{
				// Push the current maximized state into the SystemWindow where it can be used or persisted by Agg applications
				AggSystemWindow.Maximized = this.WindowState == FormWindowState.Maximized;
			}

			AggSystemWindow.Invalidate();

			base.OnResize(e);
		}

		protected override void SetVisibleCore(bool value)
		{
			// Force Activation/BringToFront behavior when Visibility enabled. This ensures Agg forms 
			// always come to front after ShowSystemWindow()
			if (value)
			{
				this.Activate();
			}

			base.SetVisibleCore(value);
		}

		private bool waitingForIdleTimerToStop = false;

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			// Call on closing and check if we can close (a "do you want to save" might cancel the close. :).
			bool cancelClose = false;

			if (AggSystemWindow != null && !AggSystemWindow.HasBeenClosed)
			{
				AggSystemWindow.OnClosing(out cancelClose);

				if (cancelClose)
				{
					e.Cancel = true;
				}
				else
				{
					if (!AggSystemWindow.HasBeenClosed)
					{
						AggSystemWindow.Close();
					}

					if (this.IsMainWindow && !waitingForIdleTimerToStop)
					{
						waitingForIdleTimerToStop = true;
						idleCallBackTimer.Stop();
						idleCallBackTimer.Elapsed -= InvokePendingOnIdleActions;
						e.Cancel = true;
						// We just need to wait for this event to end so we can re-enter the idle loop with the time stopped
						// If we close with the idle loop timer not stopped we throw and exception.
						System.Windows.Forms.Timer delayedCloseTimer = new System.Windows.Forms.Timer();
						delayedCloseTimer.Tick += DoDelayedClose;
						delayedCloseTimer.Start();
					}
				}
			}

			base.OnClosing(e);
		}

		private void DoDelayedClose(object sender, EventArgs e)
		{
			((System.Windows.Forms.Timer)sender).Stop();
			this.Close();
		}

		#region WidgetForWindowsFormsAbstract/WinformsWindowWidget
		#endregion

		#region IPlatformWindow

		public new Agg.UI.Keys ModifierKeys => (Agg.UI.Keys)Control.ModifierKeys;

		/*
		 * 
		 * // Can't simply override BringToFront. Change Interface method name/signature if required. Leaving as is
		 * // to call base/this.BringToFront via Interface call
		public override void BringToFront()
		{
			// Numerous articles on the web claim that Activate does not always bring to front (something MatterControl
			// suffers when running automation tests). If we were to continue to use Activate, we might consider calling
			// BringToFront after
			this.Activate();
			this.BringToFront();
		}*/

		// TODO: Why is this member named Caption instead of Title?
		public string Caption
		{
			get
			{
				return this.Text;
			}
			set
			{
				this.Text = value;
			}
		}

		public Point2D DesktopPosition
		{
			get
			{
				return new Point2D(this.DesktopLocation.X, this.DesktopLocation.Y);
			}

			set
			{
				if (!this.Visible)
				{
					this.StartPosition = FormStartPosition.Manual;
				}

				this.DesktopLocation = new Point(value.x, value.y);
			}
		}

		public new void Show()
		{
			// Center the window if specified on the SystemWindow
			if (MainWindowsFormsWindow != this && systemWindow.CenterInParent)
			{
				Rectangle desktopBounds = MainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = systemWindow.LocalBounds;

				this.Left = desktopBounds.X + desktopBounds.Width / 2 - (int)newItemBounds.Width / 2;
				this.Top = desktopBounds.Y + desktopBounds.Height / 2 - (int)newItemBounds.Height / 2 - TitleBarHeight / 2;
			}

			if (MainWindowsFormsWindow != this
				&& systemWindow.AlwaysOnTopOfMain)
			{
				base.Show(MainWindowsFormsWindow);
			}
			else
			{
				base.Show();
			}
		}

		public void ShowModal()
		{
			// Release the onidle guard so that the onidle pump continues processing while we block at ShowDialog below
			Task.Run(() => this.ReleaseOnIdleGuard());

			if (MainWindowsFormsWindow != this && systemWindow.CenterInParent)
			{
				Rectangle mainBounds = MainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = systemWindow.LocalBounds;

				this.Left = mainBounds.X + mainBounds.Width / 2 - (int)newItemBounds.Width / 2;
				this.Top = mainBounds.Y + mainBounds.Height / 2 - (int)newItemBounds.Height / 2;
			}

			this.ShowDialog();
		}

		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			// Cascade Agg invalidates to Winforms
			//
			// TODO: Evaluate the cost of this action
			this.Invalidate();
		}

		/*
		 * 
		 * // Region based invalidate is pointless given in OnPaint we call OnDraw on the Agg root
		 * // i.e. We always redraw everything. Region has no effect
		
		public override void Invalidate(RectangleDouble rectToInvalidate)
		{
			rectToInvalidate.IntersectWithRectangle(LocalBounds);

			//rectToInvalidate = new rect_d(0, 0, Width, Height);

			Rectangle windowsRectToInvalidate = GetRectangleFromRectD(rectToInvalidate);
			if (WindowsFormsWindow != null)
			{
				// This looks like a positive feedback loop
				WindowsFormsWindow.RequestInvalidate(windowsRectToInvalidate);
			}
		}

		public Rectangle GetRectangleFromRectD(RectangleDouble rectD)
		{
			Rectangle windowsRect = new Rectangle(
				(int)System.Math.Floor(rectD.Left),
				(int)System.Math.Floor(Height - rectD.Top),
				(int)System.Math.Ceiling(rectD.Width),
				(int)System.Math.Ceiling(rectD.Height));

			return windowsRect;
		}
		 */

		public virtual void BoundsChanged(EventArgs e)
		{
			// Cascade Agg SystemWindow size change to Winforms
			//
			// TODO: It seems likely that we tell Winforms to resize, then Winforms tells us a to resize, then... ?
			this.Invalidate();
		}

		public void SetCursor(Cursors cursorToSet)
		{
			switch (cursorToSet)
			{
				case Cursors.Arrow:
					this.Cursor = System.Windows.Forms.Cursors.Arrow;
					break;

				case Cursors.Hand:
					this.Cursor = System.Windows.Forms.Cursors.Hand;
					break;

				case Cursors.IBeam:
					this.Cursor = System.Windows.Forms.Cursors.IBeam;
					break;
			}
		}

		public int TitleBarHeight { get; private set; } = 0;

		#endregion

		#region Agg Event Proxies
		/*
		protected override void OnMouseLeave(EventArgs e)
		{
			SystemWindow.OnMouseMove(new MatterHackers.Agg.UI.MouseEventArgs(MatterHackers.Agg.UI.MouseButtons.None, 0, -10, -10, 0));
			base.OnMouseLeave(e);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			SystemWindow.OnFocusChanged(e);
			base.OnGotFocus(e);
		}

		protected override void OnLostFocus(EventArgs e)
		{
			SystemWindow.Unfocus();
			SystemWindow.OnFocusChanged(e);

			base.OnLostFocus(e);
		}

		protected override void OnKeyDown(System.Windows.Forms.KeyEventArgs e)
		{
			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent;
			if (OsInformation.OperatingSystem == OSType.Mac
				&& (e.KeyData & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)(System.Windows.Forms.Keys.Control | (e.KeyData & ~System.Windows.Forms.Keys.Alt)));
			}
			else
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)e.KeyData);
			}
			SystemWindow.OnKeyDown(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, true);

			e.Handled = aggKeyEvent.Handled;
			e.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;

			base.OnKeyDown(e);
		}

		protected override void OnKeyUp(System.Windows.Forms.KeyEventArgs e)
		{
			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)e.KeyData);
			SystemWindow.OnKeyUp(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);

			e.Handled = aggKeyEvent.Handled;
			e.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;

			base.OnKeyUp(e);
		}

		protected override void OnKeyPress(System.Windows.Forms.KeyPressEventArgs e)
		{
			MatterHackers.Agg.UI.KeyPressEventArgs aggKeyPressEvent = new MatterHackers.Agg.UI.KeyPressEventArgs(e.KeyChar);
			SystemWindow.OnKeyPress(aggKeyPressEvent);
			e.Handled = aggKeyPressEvent.Handled;

			base.OnKeyPress(e);
		}

		private MatterHackers.Agg.UI.MouseEventArgs ConvertWindowsMouseEventToAggMouseEvent(System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// we invert the y as we are bottom left coordinate system and windows is top left.
			int Y = windowsMouseEvent.Y;
			Y = (int)SystemWindow.BoundsRelativeToParent.Height - Y;

			return new MatterHackers.Agg.UI.MouseEventArgs((MatterHackers.Agg.UI.MouseButtons)windowsMouseEvent.Button, windowsMouseEvent.Clicks, windowsMouseEvent.X, Y, windowsMouseEvent.Delta);
		}

		protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e)
		{
			SystemWindow.OnMouseDown(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
		{
			// TODO: Remove short term workaround for automation issues where mouse events fire differently if mouse is within window region
			if (!EnableInputHook)
			{
				return;
			}

			SystemWindow.OnMouseMove(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
		{
			SystemWindow.OnMouseUp(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseUp(e);
		}

		protected override void OnMouseCaptureChanged(EventArgs e)
		{
			if (SystemWindow.ChildHasMouseCaptured || SystemWindow.MouseCaptured)
			{
				SystemWindow.OnMouseUp(new MatterHackers.Agg.UI.MouseEventArgs(Agg.UI.MouseButtons.Left, 0, -10, -10, 0));
			}
			base.OnMouseCaptureChanged(e);
		}

		protected override void OnMouseWheel(System.Windows.Forms.MouseEventArgs e)
		{
			SystemWindow.OnMouseWheel(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseWheel(e);
		}
		*/
		#endregion

		public static WinformsSystemWindow MainWindowsFormsWindow { get; private set; }

		public new Vector2 MinimumSize
		{
			get
			{
				return new Vector2(base.MinimumSize.Width, base.MinimumSize.Height);
			}
			set
			{
				Size clientSize = new Size((int)Math.Ceiling(value.x), (int)Math.Ceiling(value.y));

				Size windowSize = new Size(
					clientSize.Width + this.Width - this.ClientSize.Width,
					clientSize.Height + this.Height - this.ClientSize.Height);

				base.MinimumSize = windowSize;
			}
		}

		private bool pendingSetInitialDesktopPosition = false;
		private Point2D InitialDesktopPosition = new Point2D();
		private static bool firstWindow = true;

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			// TODO: Now done at construction time, verify no issue
			// osMappingWindow.Caption = systemWindow.Title;
			// osMappingWindow.MinimumSize = systemWindow.MinimumSize;


			if (SingleWindowMode)
			{
				// Store the active SystemWindow
				allOpenSystemWindows.Push(systemWindow);
			}

			// Set the active SystemWindow
			this.AggSystemWindow = systemWindow;

			if (pendingSetInitialDesktopPosition)
			{
				// and make sure the title is correct right now
				pendingSetInitialDesktopPosition = false;
				systemWindow.DesktopPosition = InitialDesktopPosition;
			}

			systemWindow.AnchorAll();

			if (firstWindow)
			{
				firstWindow = false;

				this.Show();
				Application.Run(this);
			}
			else if (!SingleWindowMode)
			{
				if (systemWindow.IsModal)
				{
					this.ShowModal();
				}
				else
				{
					this.Show();
					this.BringToFront();
				}
			}
			else if (SingleWindowMode)
			{
				// Notify the embedded window of its new single windows parent size
				// TODO: Hack - figure out how to push this into non-firstWindow items
				//systemWindow.Size = new Vector2(this.Size.Width, this.Size.Height);

				systemWindow.Size = new Vector2(
						this.ClientSize.Width,
						this.ClientSize.Height);
				//systemWindow.Position = Vector2.Zero;
			}
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			if (SingleWindowMode)
			{
				// Remove the closing window
				allOpenSystemWindows.Pop();

				// Restore the prior window from the stack
				AggSystemWindow = allOpenSystemWindows.Count <= 0 ? null : allOpenSystemWindows.Peek();
				AggSystemWindow?.Invalidate();
			}
			else
			{
				this.Close();
			}
		}

#if DEBUG
		public abstract class FormInspector : Form
		{
			public virtual bool Inspecting { get; set; }
		}

		public static Func<SystemWindow, FormInspector> InspectorCreator { get; set; }
#endif
	}
}
