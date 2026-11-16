namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 相机模式。
    /// 当前只落地自由视角；后续的锁定视角 / 剧情过场等模式在此扩展，切换逻辑统一走 CameraModule。
    /// </summary>
    public enum CameraMode
    {
        /// <summary>第三人称自由视角：跟随目标 + 鼠标环绕/俯仰。</summary>
        FreeLook = 0,
    }
}
