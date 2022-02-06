import fs from "fs/promises";
import { IParticle, ISave } from "./types";

const SAVED_PATH = "C:\\Users\\eranp\\saveddata\\";
const IN_FILE = "2022-01-26T19-48";

function disassemble(s: ISave, groupI: number): ISave {
  const { feas, freeParticles, ...rest } = s;
  const fea = s.feas[groupI];
  const reastFeas = feas.slice(0, groupI).concat(feas.slice(groupI + 1));
  // TODO
  // hiddenNodes position should account for s.lastPos and transform it to new position after linear transforamtion
  const feaParticles: IParticle[] = [
    ...fea.hiddenNodes.map((s) => s.p),
    ...fea.currentPolygon.slice(1).map((s) => s.go),
  ];
  return {
    ...rest,
    feas: reastFeas,
    freeParticles: [...freeParticles, ...feaParticles],
  };
}

async function p() {
  const inF = `${SAVED_PATH}${IN_FILE}.json`;
  const outF = `${SAVED_PATH}${IN_FILE}_disassembly.json`;
  const saved: ISave = JSON.parse(await fs.readFile(inF, "utf-8"));

  const outRep = disassemble(saved, 0);
  fs.writeFile(outF, JSON.stringify(outRep, null, 4));
}
p();
