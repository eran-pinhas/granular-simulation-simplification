export type IXY = { x: number; y: number };
export type IParticle = {
  id: number;
  position: IXY;
  localScale: [number, number, number];
};

export type ISave = {
  ParticleCounter: number;
  createdCount: number;
  // FeasCounter: number;
  freeParticles: IParticle[];

  feas: {
    hiddenNodes: { p: IParticle; lastPosition: IXY }[];
    outerLinks: { particleId: number; toPoint: IXY }[];
    innerLinks: { fromPoint: IXY; toPoint: IXY }[];
    innerMeshElements: { pos: IXY; lastPos: IXY }[];
    currentPolygon: { positionsInRootRS: IXY; go: IParticle }[];
  }[];
};
