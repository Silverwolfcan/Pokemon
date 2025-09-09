using UnityEngine;
using System;

public class CombatBoundary : MonoBehaviour
{
    private Func<Vector3> getPlayerPos;
    private Action<Vector3> setPlayerPos;
    private Func<Vector3> getCenter;
    private float radius;

    public void Setup(Func<Vector3> getPlayerPos, Action<Vector3> setPlayerPos,
                      Func<Vector3> getCenter, float radius)
    {
        this.getPlayerPos = getPlayerPos;
        this.setPlayerPos = setPlayerPos;
        this.getCenter = getCenter;
        this.radius = radius;
    }

    private void LateUpdate()
    {
        if (getPlayerPos == null || setPlayerPos == null || getCenter == null) return;

        var pos = getPlayerPos();
        var c = getCenter();
        var flat = pos; flat.y = 0f;
        var flatC = c; flatC.y = 0f;

        var v = flat - flatC;
        var d = v.magnitude;
        if (d > radius)
        {
            var clamped = flatC + v.normalized * radius;
            clamped.y = pos.y;
            setPlayerPos(clamped);
        }
    }
}
