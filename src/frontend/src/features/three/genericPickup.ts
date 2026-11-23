import * as T from 'three'
import { themeColor } from '../../theme/sceneTheme'
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js'
export function createPickup(){
 const car=new T.Group()
 const paint=new T.MeshStandardMaterial({color:themeColor('--generic-paint'),metalness:.6,roughness:.32})
 const dark=new T.MeshStandardMaterial({color:themeColor('--generic-trim'),roughness:.7})
 const glass=new T.MeshStandardMaterial({color:themeColor('--generic-glass'),metalness:.55,roughness:.15})
 const chrome=new T.MeshStandardMaterial({color:themeColor('--generic-chrome'),metalness:.85,roughness:.28})
 const lamp=new T.MeshStandardMaterial({color:themeColor('--generic-lamp'),emissive:themeColor('--generic-lamp-emissive'),emissiveIntensity:.35})
 function mesh(geometry:T.BufferGeometry,material:T.Material,x:number,y:number,z:number,key='body'){
  const m=new T.Mesh(geometry,material);m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;m.userData.componentKey=key;m.userData.paintable=material===paint;car.add(m);return m
 }
 function box(w:number,h:number,d:number,x:number,y:number,z:number,mat=paint,key='body'){return mesh(new RoundedBoxGeometry(w,h,d,2,.045),mat,x,y,z,key)}
 box(4.5,.28,1.72,0,.62,0,dark)
 box(4.55,.48,1.78,0,.92,0)
 box(1.44,.26,1.76,1.45,1.26,0,paint,'engine')
 box(1.95,.12,1.65,-1.25,1.17,0,dark)
 for(const z of [-.82,.82])box(1.9,.38,.13,-1.27,1.33,z)
 box(.13,.4,1.77,-2.19,1.33,0)
 const shape=new T.Shape();shape.moveTo(-.52,1.12);shape.lineTo(-.52,2.08);shape.quadraticCurveTo(-.52,2.16,-.38,2.16);shape.lineTo(.43,2.16);shape.quadraticCurveTo(.53,2.16,.58,2.06);shape.lineTo(1.07,1.26);shape.closePath()
 const cab=mesh(new T.ExtrudeGeometry(shape,{depth:1.62,bevelEnabled:true,bevelSize:.04,bevelThickness:.04,bevelSegments:2,steps:1}),paint,0,0,-.81)
 cab.userData.componentKey='body'
 for(const z of [-.858,.858]){
  const s=new T.Shape();s.moveTo(-.38,1.49);s.lineTo(-.38,2.01);s.lineTo(.42,2.01);s.lineTo(.77,1.49);s.closePath()
  const window=mesh(new T.ShapeGeometry(s),glass,0,0,z);(window.material as T.MeshStandardMaterial).side=T.DoubleSide
  box(.19,.045,.025,-.19,1.32,z,chrome)
  box(.23,.16,.2,.72,1.54,z*1.12,dark)
 }
 const wind=box(.96,.025,1.4,.77,1.72,0,glass);wind.rotation.z=-1.01
 box(.022,.55,1.4,-.57,1.76,0,glass)
 for(const x of [-1.4,1.38])for(const z of [-.91,.91]){
  const tire=mesh(new T.CylinderGeometry(.47,.47,.27,40),dark,x,.49,z,'wheels');tire.rotation.x=Math.PI/2
  const rim=mesh(new T.CylinderGeometry(.305,.305,.29,24),chrome,x,.49,z,'wheels');rim.rotation.x=Math.PI/2
  const center=mesh(new T.CylinderGeometry(.11,.11,.31,16),dark,x,.49,z,'brakes');center.rotation.x=Math.PI/2
  for(let a=0;a<5;a++){const spoke=box(.08,.49,.025,x,.49,z+(z>0?.15:-.15),dark,'wheels');spoke.rotation.z=a*Math.PI*2/5}
  const fender=mesh(new T.TorusGeometry(.5,.045,8,32,Math.PI),paint,x,.49,z,'suspension');fender.rotation.z=0
 }
 box(.11,.34,1.78,2.3,.83,0,dark)
 box(.05,.26,.86,2.32,1.1,0,dark,'cooling')
 for(const z of [-.64,.64])box(.07,.23,.4,2.32,1.12,z,lamp,'lights')
 for(const z of [-.72,.72])box(.07,.3,.19,-2.28,1.2,z,new T.MeshStandardMaterial({color:themeColor('--generic-tail-light'),emissive:themeColor('--generic-tail-emissive')}),'lights')
 box(.12,.15,1.8,-2.31,.74,0,dark)
 box(.36,.1,.12,-2.2,.4,.61,chrome,'exhaust')
 return car
}
