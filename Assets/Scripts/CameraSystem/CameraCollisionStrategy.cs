namespace CCCProject.CameraSystem
{
    /// <summary>
    /// 相机被遮挡时的解决方式（对外抽象，不暴露 Cinemachine 的枚举）。
    /// </summary>
    public enum CameraCollisionStrategy
    {
        /// <summary>沿视线把相机拉近：视角朝向始终等于输入，臂长随遮挡变短。</summary>
        PullIn = 0,

        /// <summary>贴着障碍面滑动：尽量保住相机高度与距离，但朝向会跟不上输入。</summary>
        SlideAlongWall = 1,
    }
}
