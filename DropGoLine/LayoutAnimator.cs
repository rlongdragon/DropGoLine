using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DropGoLine {
  /// <summary>
  /// 負責管理控制項的平滑佈局動畫
  /// </summary>
  public class LayoutAnimator {
    // 動畫設定
    private const int INTERVAL = 15; // Timer 間隔 (ms), 約 60 FPS
    private const int DURATION = 400; // 動畫總時間 (ms)

    private System.Windows.Forms.Timer animationTimer;

    // 記錄每個控制項的動畫狀態
    private class AnimationInfo {
      public Control TargetControl = null!; // Suppress warning, assigned in Animate
      public Rectangle StartBounds;
      public Rectangle EndBounds;
      public long StartTime;
      public float Delay = 0f; // Assign default
    }

    private List<AnimationInfo> activeAnimations = new List<AnimationInfo>();
    private bool isAnimating = false;

    public LayoutAnimator() {
      animationTimer = new System.Windows.Forms.Timer();
      animationTimer.Interval = INTERVAL;
      animationTimer.Tick += AnimationTimer_Tick;
    }

    /// <summary>
    /// 開始一個控制項的動畫
    /// </summary>
    /// <param name="control">目標控制項</param>
    /// <param name="targetBounds">目標位置與大小</param>
    public void Animate(Control control, Rectangle targetBounds) {
      // 如果該控制項已經在動畫清單中，先移除舊的 (更新目標)
      activeAnimations.RemoveAll(a => a.TargetControl == control);

      var info = new AnimationInfo {
        TargetControl = control,
        StartBounds = control.Bounds,
        EndBounds = targetBounds,
        StartTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond,
        Delay = 0
      };

      activeAnimations.Add(info);

      if (!isAnimating) {
        isAnimating = true;
        animationTimer.Start();
      }
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e) {
      long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

      // 倒序走訪以便安全移除完成的動畫
      for (int i = activeAnimations.Count - 1; i >= 0; i--) {
        var anim = activeAnimations[i];
        long elapsed = now - anim.StartTime;

        if (elapsed >= DURATION) {
          // 動畫結束，直接設為目標值
          anim.TargetControl.Bounds = anim.EndBounds;
          activeAnimations.RemoveAt(i);
        } else {
          // 計算進度 0.0 ~ 1.0
          float t = (float)elapsed / DURATION;

          // 套用 Easing 函數 (Cubic Ease Out)
          float ease = CubicEaseOut(t);

          // 內插計算新位置
          int x = Lerp(anim.StartBounds.X, anim.EndBounds.X, ease);
          int y = Lerp(anim.StartBounds.Y, anim.EndBounds.Y, ease);
          int w = Lerp(anim.StartBounds.Width, anim.EndBounds.Width, ease);
          int h = Lerp(anim.StartBounds.Height, anim.EndBounds.Height, ease);

          anim.TargetControl.SetBounds(x, y, w, h);
        }
      }

      // 如果沒有動畫了，停止 Timer 節省資源
      if (activeAnimations.Count == 0) {
        isAnimating = false;
        animationTimer.Stop();
      }
    }

    // Cubic Ease Out: 一開始快，最後慢慢煞車
    private float CubicEaseOut(float t) {
      return 1 - (float)Math.Pow(1 - t, 3);
    }

    // 線性內插
    private int Lerp(int start, int end, float t) {
      return (int)(start + (end - start) * t);
    }
  }
}
