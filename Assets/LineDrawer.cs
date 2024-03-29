﻿using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System;

public class LineDrawer
{

    public LineRenderer lr;
    public GameObject line;
    Vector3 shift;
    public LineDrawer(Vector3 shift , Color color)
    {
        this.shift = shift;

        line = new GameObject("line-drawer-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        lr = line.AddComponent<LineRenderer>();
        lr.startWidth = 0.5f;
        lr.endWidth = 0.5f;
        setColor(color);
        line.transform.parent = GameObject.Find("DrawedLines").transform;
        
    }


    public void setColor(Color color){
        lr.startColor = color;
        lr.endColor = color;
        lr.material.color = color;
    }

    private List<Particle> particles = new List<Particle>();

    public void updatePositions()
    {
        var positions = particles.Select(p => p.gameObject.transform.position + shift);
        lr.SetPositions(positions.ToArray());
    }
    public void setPoints(IEnumerable<Particle> l)
    {
        particles.Clear();
        particles.AddRange(l);
        lr.positionCount = particles.Count;
    }
}
