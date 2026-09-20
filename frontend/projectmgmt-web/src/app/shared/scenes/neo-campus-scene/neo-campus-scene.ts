import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  NgZone,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { gsap } from 'gsap';
import { MotionPreferencesService } from '../../../core/motion/motion-preferences.service';
import { SceneQuality, SceneQualityService } from '../../../core/motion/scene-quality.service';

export type NeoCampusMode = 'LOGIN' | 'SIGNUP' | 'OTP_REGISTER' | 'FORGOT' | 'OTP_FORGOT' | 'COMMAND' | 'AI_LAB';
export type NeoCampusVariant = 'auth' | 'command' | 'ai';

type ThreeModule = typeof import('three');
type QuickTo = ((value: number) => void) | null;

@Component({
  selector: 'app-neo-campus-scene',
  standalone: true,
  templateUrl: './neo-campus-scene.html',
  styleUrl: './neo-campus-scene.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NeoCampusSceneComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('sceneHost', { static: true }) private hostRef!: ElementRef<HTMLElement>;
  @ViewChild('sceneCanvas', { static: true }) private canvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() mode: NeoCampusMode = 'LOGIN';
  @Input() variant: NeoCampusVariant = 'auth';

  private readonly zone = inject(NgZone);
  private readonly preferences = inject(MotionPreferencesService);
  private readonly qualityService = inject(SceneQualityService);

  private THREE: ThreeModule | null = null;
  private renderer: import('three').WebGLRenderer | null = null;
  private scene: import('three').Scene | null = null;
  private camera: import('three').PerspectiveCamera | null = null;
  private clock: import('three').Clock | null = null;
  private world: import('three').Group | null = null;
  private robot: import('three').Group | null = null;
  private drone: import('three').Group | null = null;
  private hologram: import('three').Group | null = null;
  private extraCharacter: import('three').Group | null = null;
  private securityBeacon: import('three').Group | null = null;
  private particles: import('three').Points | null = null;
  private characters: import('three').Group[] = [];
  private taskBlocks: import('three').Object3D[] = [];
  private interactiveObjects: import('three').Object3D[] = [];
  private hoveredObject: import('three').Object3D | null = null;
  private raycaster: import('three').Raycaster | null = null;
  private pointer: import('three').Vector2 | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private intersectionObserver: IntersectionObserver | null = null;
  private storyTimeline: gsap.core.Timeline | null = null;
  private cameraXTo: QuickTo = null;
  private cameraYTo: QuickTo = null;
  private destroyed = false;
  private inViewport = true;
  private quality: SceneQuality = 'static';

  readonly ready = signal(false);
  readonly fallback = signal(false);
  readonly label = signal('Scrum Lab đang hoạt động');

  ngAfterViewInit(): void {
    this.quality = this.qualityService.quality();
    if (this.quality === 'static') {
      this.fallback.set(true);
      this.ready.set(true);
      this.syncLabel();
      return;
    }

    this.zone.runOutsideAngular(() => void this.initializeScene());
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['mode']) {
      this.syncLabel();
      if (this.world) this.applyMode(true);
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.storyTimeline?.kill();
    this.resizeObserver?.disconnect();
    this.intersectionObserver?.disconnect();
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerleave', this.onPointerLeave);
    document.removeEventListener('visibilitychange', this.syncLoopState);
    this.canvasRef.nativeElement.removeEventListener('webglcontextlost', this.onContextLost);

    if (this.camera) gsap.killTweensOf(this.camera.position);
    if (this.hoveredObject) gsap.killTweensOf(this.hoveredObject.scale);

    this.renderer?.setAnimationLoop(null);
    this.disposeScene();
  }

  private async initializeScene(): Promise<void> {
    try {
      const THREE = await import('three');
      if (this.destroyed) return;
      this.THREE = THREE;

      const canvas = this.canvasRef.nativeElement;
      const renderer = new THREE.WebGLRenderer({
        canvas,
        alpha: true,
        antialias: this.quality === 'full',
        powerPreference: 'high-performance'
      });
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, this.quality === 'full' ? 1.5 : 1.15));
      renderer.outputColorSpace = THREE.SRGBColorSpace;
      renderer.toneMapping = THREE.ACESFilmicToneMapping;
      renderer.toneMappingExposure = 1.08;
      renderer.shadowMap.enabled = this.quality === 'full';
      renderer.shadowMap.type = THREE.PCFSoftShadowMap;
      this.renderer = renderer;

      const scene = new THREE.Scene();
      scene.fog = new THREE.FogExp2(0x0a2d65, this.variant === 'auth' ? 0.035 : 0.055);
      this.scene = scene;

      const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 100);
      camera.position.set(7.6, 5.3, 10.8);
      this.camera = camera;
      this.clock = new THREE.Clock();
      this.raycaster = new THREE.Raycaster();
      this.pointer = new THREE.Vector2(0, 0);

      this.buildWorld(THREE);
      this.setupLights(THREE);
      this.setupObservers();
      this.resize();
      this.applyMode(false);
      this.setupPointerMotion();

      this.syncLoopState();
      this.ready.set(true);
    } catch {
      if (!this.destroyed) {
        this.fallback.set(true);
        this.ready.set(true);
      }
    }
  }

  private buildWorld(THREE: ThreeModule): void {
    if (!this.scene) return;

    const world = new THREE.Group();
    world.rotation.y = -0.12;
    world.position.set(this.variant === 'auth' ? -1.35 : 0, -1.05, 0);
    this.world = world;
    this.scene.add(world);

    const groundMaterial = new THREE.MeshStandardMaterial({ color: 0x0c3f83, roughness: 0.72, metalness: 0.08 });
    const ground = new THREE.Mesh(new THREE.CylinderGeometry(6.4, 6.8, 0.48, 10), groundMaterial);
    ground.receiveShadow = true;
    world.add(ground);

    const innerPlatform = new THREE.Mesh(
      new THREE.CylinderGeometry(5.35, 5.55, 0.18, 10),
      new THREE.MeshStandardMaterial({ color: 0x1456a8, roughness: 0.6, metalness: 0.16 })
    );
    innerPlatform.position.y = 0.3;
    innerPlatform.receiveShadow = true;
    world.add(innerPlatform);

    this.createCampusBuildings(THREE, world);
    this.createScrumLab(THREE, world);
    this.createNature(THREE, world);
    this.createCharacters(THREE, world);
    this.robot = this.createRobot(THREE);
    this.robot.position.set(1.5, 1.18, 1.15);
    world.add(this.robot);
    this.interactiveObjects.push(this.robot);

    this.drone = this.createDrone(THREE);
    this.drone.position.set(-1.4, 3.45, -0.8);
    world.add(this.drone);

    this.hologram = this.createHologram(THREE);
    this.hologram.position.set(1.45, 2.1, 1.12);
    world.add(this.hologram);

    this.securityBeacon = this.createSecurityBeacon(THREE);
    this.securityBeacon.position.set(3.85, 0.75, -1.45);
    this.securityBeacon.scale.setScalar(0.001);
    world.add(this.securityBeacon);

    this.particles = this.createParticles(THREE);
    world.add(this.particles);
  }

  private createCampusBuildings(THREE: ThreeModule, parent: import('three').Group): void {
    const buildingMaterial = new THREE.MeshStandardMaterial({ color: 0xdbeafe, roughness: 0.76 });
    const accentMaterial = new THREE.MeshStandardMaterial({ color: 0x1d4ed8, roughness: 0.5, metalness: 0.15 });
    const glassMaterial = new THREE.MeshStandardMaterial({ color: 0x67e8f9, emissive: 0x0e7490, emissiveIntensity: 0.22, roughness: 0.2, metalness: 0.35 });
    const configs = [
      { x: -4.2, z: -2.15, w: 2.1, h: 2.9, d: 1.45 },
      { x: 0.1, z: -3.45, w: 3.6, h: 2.25, d: 1.15 },
      { x: 4.05, z: -2.35, w: 2.2, h: 3.35, d: 1.35 }
    ];

    configs.forEach((config, buildingIndex) => {
      const group = new THREE.Group();
      const body = new THREE.Mesh(new THREE.BoxGeometry(config.w, config.h, config.d), buildingMaterial);
      body.position.y = config.h / 2 + 0.48;
      body.castShadow = true;
      body.receiveShadow = true;
      group.add(body);

      const roof = new THREE.Mesh(new THREE.BoxGeometry(config.w + 0.18, 0.16, config.d + 0.18), accentMaterial);
      roof.position.y = config.h + 0.55;
      group.add(roof);

      const windowRows = buildingIndex === 1 ? 2 : 3;
      const windowColumns = buildingIndex === 1 ? 6 : 3;
      for (let row = 0; row < windowRows; row += 1) {
        for (let column = 0; column < windowColumns; column += 1) {
          const windowMesh = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.32, 0.035), glassMaterial);
          windowMesh.position.set(
            -config.w / 2 + 0.4 + column * ((config.w - 0.8) / Math.max(1, windowColumns - 1)),
            1.02 + row * 0.68,
            config.d / 2 + 0.025
          );
          group.add(windowMesh);
        }
      }
      group.position.set(config.x, 0, config.z);
      parent.add(group);
    });
  }

  private createScrumLab(THREE: ThreeModule, parent: import('three').Group): void {
    const board = new THREE.Group();
    board.position.set(-1.35, 1.75, -1.48);
    const frame = new THREE.Mesh(
      new THREE.BoxGeometry(3.2, 1.72, 0.14),
      new THREE.MeshStandardMaterial({ color: 0xeaf3ff, roughness: 0.42, metalness: 0.08 })
    );
    frame.castShadow = true;
    board.add(frame);

    const columnColors = [0x60a5fa, 0x818cf8, 0xc084fc, 0x34d399];
    for (let column = 0; column < 4; column += 1) {
      const rail = new THREE.Mesh(
        new THREE.BoxGeometry(0.035, 1.42, 0.035),
        new THREE.MeshBasicMaterial({ color: 0x93c5fd, transparent: true, opacity: 0.5 })
      );
      rail.position.set(-1.14 + column * 0.76, 0, 0.09);
      board.add(rail);

      for (let row = 0; row < 2; row += 1) {
        const card = new THREE.Mesh(
          new THREE.BoxGeometry(0.58, 0.34, 0.055),
          new THREE.MeshStandardMaterial({
            color: columnColors[column],
            emissive: columnColors[column],
            emissiveIntensity: 0.12,
            roughness: 0.46
          })
        );
        card.position.set(-1.13 + column * 0.76, 0.36 - row * 0.54, 0.13);
        card.userData['home'] = card.position.clone();
        card.userData['phase'] = column * 0.7 + row;
        board.add(card);
        this.taskBlocks.push(card);
        this.interactiveObjects.push(card);
      }
    }
    parent.add(board);

    const deskMaterial = new THREE.MeshStandardMaterial({ color: 0x9a6b45, roughness: 0.72 });
    const deviceMaterial = new THREE.MeshStandardMaterial({ color: 0x102a56, metalness: 0.4, roughness: 0.32 });
    const screenMaterial = new THREE.MeshStandardMaterial({ color: 0x22d3ee, emissive: 0x0891b2, emissiveIntensity: 0.85 });
    [[-2.35, 0.95], [0.25, 0.35], [2.65, -0.25]].forEach(([x, z], index) => {
      const desk = new THREE.Group();
      const top = new THREE.Mesh(new THREE.BoxGeometry(1.45, 0.13, 0.72), deskMaterial);
      top.position.y = 0.92;
      top.castShadow = true;
      desk.add(top);
      [-0.56, 0.56].forEach(legX => {
        const leg = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.86, 0.1), deskMaterial);
        leg.position.set(legX, 0.46, 0);
        desk.add(leg);
      });
      const laptop = new THREE.Mesh(new THREE.BoxGeometry(0.72, 0.48, 0.055), deviceMaterial);
      laptop.position.set(0, 1.25, -0.1);
      laptop.rotation.x = -0.14;
      desk.add(laptop);
      const screen = new THREE.Mesh(new THREE.BoxGeometry(0.61, 0.36, 0.018), screenMaterial);
      screen.position.set(0, 1.25, -0.132);
      screen.rotation.x = -0.14;
      desk.add(screen);
      desk.position.set(x, 0, z);
      desk.rotation.y = index === 2 ? -0.38 : index === 0 ? 0.24 : -0.08;
      parent.add(desk);
    });
  }

  private createNature(THREE: ThreeModule, parent: import('three').Group): void {
    const treePositions = [
      [-5.2, -1.1], [-4.8, 1.9], [-3.6, 3.5], [-0.4, 4.7],
      [2.45, 4.0], [4.65, 2.55], [5.35, 0.35], [4.9, -1.25]
    ];
    const trunkGeometry = new THREE.CylinderGeometry(0.08, 0.12, 0.72, 6);
    const crownGeometry = new THREE.IcosahedronGeometry(0.46, 0);
    const trunks = new THREE.InstancedMesh(
      trunkGeometry,
      new THREE.MeshStandardMaterial({ color: 0x815b3c, roughness: 0.88 }),
      treePositions.length
    );
    const crowns = new THREE.InstancedMesh(
      crownGeometry,
      new THREE.MeshStandardMaterial({ color: 0x34d399, roughness: 0.7 }),
      treePositions.length
    );
    const matrix = new THREE.Matrix4();
    treePositions.forEach(([x, z], index) => {
      matrix.makeTranslation(x, 0.76, z);
      trunks.setMatrixAt(index, matrix);
      matrix.compose(
        new THREE.Vector3(x, 1.46, z),
        new THREE.Quaternion(),
        new THREE.Vector3(1 + (index % 3) * 0.08, 1, 1 + (index % 2) * 0.08)
      );
      crowns.setMatrixAt(index, matrix);
    });
    trunks.castShadow = this.quality === 'full';
    crowns.castShadow = this.quality === 'full';
    parent.add(trunks, crowns);

    const cloudMaterial = new THREE.MeshStandardMaterial({ color: 0xffffff, transparent: true, opacity: 0.62, roughness: 1 });
    for (let index = 0; index < (this.quality === 'lite' ? 2 : 4); index += 1) {
      const cloud = new THREE.Group();
      for (let puff = 0; puff < 3; puff += 1) {
        const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.42 + puff * 0.06, 8, 6), cloudMaterial);
        mesh.position.set((puff - 1) * 0.42, Math.abs(puff - 1) * 0.12, 0);
        cloud.add(mesh);
      }
      cloud.position.set(-4.5 + index * 3.2, 4.5 + (index % 2) * 0.5, -3.2 - index * 0.35);
      cloud.userData['phase'] = index * 1.7;
      cloud.userData['cloud'] = true;
      parent.add(cloud);
    }
  }

  private createCharacters(THREE: ThreeModule, parent: import('three').Group): void {
    const configs = [
      { color: 0x2563eb, skin: 0xf0b38e, position: [-2.25, 0.58, 1.55] as const, phase: 0.2 },
      { color: 0x7c3aed, skin: 0xc77f58, position: [0.1, 0.58, 1.0] as const, phase: 1.35 },
      { color: 0x0f766e, skin: 0xe5a77d, position: [2.45, 0.58, 0.42] as const, phase: 2.4 },
      { color: 0xdb2777, skin: 0x9a5f3f, position: [-0.6, 0.58, -0.35] as const, phase: 3.6 }
    ];

    configs.slice(0, this.quality === 'lite' ? 3 : configs.length).forEach((config, index) => {
      const character = this.createCharacter(THREE, config.color, config.skin, index % 2 === 0);
      character.position.set(config.position[0], config.position[1], config.position[2]);
      character.rotation.y = index === 2 ? -0.55 : index === 0 ? 0.22 : 0;
      character.userData['baseY'] = character.position.y;
      character.userData['phase'] = config.phase;
      parent.add(character);
      this.characters.push(character);
    });

    if (this.quality !== 'lite') {
      const extra = this.createCharacter(THREE, 0xf59e0b, 0xd99368, true);
      extra.position.set(4.25, 0.58, 1.5);
      extra.userData['baseY'] = extra.position.y;
      extra.userData['phase'] = 4.8;
      extra.scale.setScalar(0.001);
      parent.add(extra);
      this.extraCharacter = extra;
      this.characters.push(extra);
    }
  }

  private createCharacter(THREE: ThreeModule, color: number, skin: number, longHair: boolean): import('three').Group {
    const group = new THREE.Group();
    const clothing = new THREE.MeshStandardMaterial({ color, roughness: 0.7 });
    const skinMaterial = new THREE.MeshStandardMaterial({ color: skin, roughness: 0.82 });
    const darkMaterial = new THREE.MeshStandardMaterial({ color: 0x172554, roughness: 0.72 });
    const whiteMaterial = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.82 });

    const torso = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.34, 0.7, 7), clothing);
    torso.position.y = 1.08;
    group.add(torso);
    const head = new THREE.Mesh(new THREE.SphereGeometry(0.255, 12, 9), skinMaterial);
    head.position.y = 1.68;
    head.userData['head'] = true;
    group.add(head);
    const hair = new THREE.Mesh(new THREE.SphereGeometry(0.265, 10, 7), darkMaterial);
    hair.position.set(0, 1.77, longHair ? -0.035 : 0);
    hair.scale.set(1.02, longHair ? 0.88 : 0.55, 1.02);
    group.add(hair);

    [-0.09, 0.09].forEach(x => {
      const eye = new THREE.Mesh(new THREE.SphereGeometry(0.022, 6, 5), darkMaterial);
      eye.position.set(x, 1.7, 0.235);
      group.add(eye);
    });

    const createLimb = (x: number, y: number, material: import('three').Material, length: number): import('three').Group => {
      const limbGroup = new THREE.Group();
      const limb = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.075, length, 6), material);
      limb.position.y = -length / 2;
      limbGroup.add(limb);
      limbGroup.position.set(x, y, 0);
      group.add(limbGroup);
      return limbGroup;
    };

    const leftArm = createLimb(-0.32, 1.35, clothing, 0.52);
    const rightArm = createLimb(0.32, 1.35, clothing, 0.52);
    const leftLeg = createLimb(-0.15, 0.76, darkMaterial, 0.58);
    const rightLeg = createLimb(0.15, 0.76, darkMaterial, 0.58);
    leftArm.rotation.z = -0.12;
    rightArm.rotation.z = 0.12;
    group.userData['leftArm'] = leftArm;
    group.userData['rightArm'] = rightArm;
    group.userData['leftLeg'] = leftLeg;
    group.userData['rightLeg'] = rightLeg;
    group.userData['headMesh'] = head;

    const badge = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.16, 0.025), whiteMaterial);
    badge.position.set(0.12, 1.16, 0.26);
    group.add(badge);
    group.traverse(object => {
      const mesh = object as import('three').Mesh;
      if (mesh.isMesh) mesh.castShadow = this.quality === 'full';
    });
    return group;
  }

  private createRobot(THREE: ThreeModule): import('three').Group {
    const robot = new THREE.Group();
    const shell = new THREE.MeshStandardMaterial({ color: 0xe0f2fe, roughness: 0.32, metalness: 0.52 });
    const blue = new THREE.MeshStandardMaterial({ color: 0x2563eb, emissive: 0x1d4ed8, emissiveIntensity: 0.42, roughness: 0.25 });
    const cyan = new THREE.MeshStandardMaterial({ color: 0x67e8f9, emissive: 0x06b6d4, emissiveIntensity: 1.1 });

    const body = new THREE.Mesh(new THREE.SphereGeometry(0.38, 14, 10), shell);
    body.scale.y = 1.15;
    robot.add(body);
    const core = new THREE.Mesh(new THREE.TorusGeometry(0.18, 0.045, 8, 20), cyan);
    core.position.z = 0.34;
    robot.add(core);
    const head = new THREE.Mesh(new THREE.BoxGeometry(0.58, 0.4, 0.42), shell);
    head.position.y = 0.61;
    robot.add(head);
    [-0.14, 0.14].forEach(x => {
      const eye = new THREE.Mesh(new THREE.SphereGeometry(0.055, 8, 6), cyan);
      eye.position.set(x, 0.64, 0.22);
      robot.add(eye);
    });
    [-0.47, 0.47].forEach((x, index) => {
      const arm = new THREE.Mesh(new THREE.CapsuleGeometry(0.07, 0.36, 4, 8), blue);
      arm.position.set(x, 0.05, 0);
      arm.rotation.z = index === 0 ? 0.48 : -0.48;
      robot.add(arm);
    });
    const hoverRing = new THREE.Mesh(new THREE.TorusGeometry(0.43, 0.035, 7, 24), cyan);
    hoverRing.rotation.x = Math.PI / 2;
    hoverRing.position.y = -0.55;
    robot.add(hoverRing);
    robot.userData['hoverRing'] = hoverRing;
    robot.traverse(object => {
      const mesh = object as import('three').Mesh;
      if (mesh.isMesh) mesh.castShadow = this.quality === 'full';
    });
    return robot;
  }

  private createDrone(THREE: ThreeModule): import('three').Group {
    const drone = new THREE.Group();
    const shell = new THREE.MeshStandardMaterial({ color: 0xcffafe, metalness: 0.48, roughness: 0.3 });
    const light = new THREE.MeshStandardMaterial({ color: 0x22d3ee, emissive: 0x0891b2, emissiveIntensity: 1.25 });
    const body = new THREE.Mesh(new THREE.SphereGeometry(0.22, 10, 7), shell);
    body.scale.set(1.5, 0.62, 1);
    drone.add(body);
    [-0.38, 0.38].forEach(x => {
      const arm = new THREE.Mesh(new THREE.BoxGeometry(0.5, 0.035, 0.05), shell);
      arm.position.x = x;
      drone.add(arm);
      const rotor = new THREE.Mesh(new THREE.TorusGeometry(0.18, 0.025, 5, 16), light);
      rotor.position.x = x * 1.55;
      rotor.rotation.x = Math.PI / 2;
      drone.add(rotor);
    });
    return drone;
  }

  private createHologram(THREE: ThreeModule): import('three').Group {
    const hologram = new THREE.Group();
    const material = new THREE.MeshBasicMaterial({ color: 0x67e8f9, transparent: true, opacity: 0.48, wireframe: true });
    [0.32, 0.48, 0.66].forEach((radius, index) => {
      const ring = new THREE.Mesh(new THREE.TorusGeometry(radius, 0.018, 5, 32), material);
      ring.rotation.set(index === 1 ? Math.PI / 2 : 0.5, index * 0.7, index * 0.45);
      ring.userData['ringSpeed'] = 0.16 + index * 0.1;
      hologram.add(ring);
    });
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.22, 1), new THREE.MeshBasicMaterial({ color: 0x22d3ee, wireframe: true }));
    hologram.add(core);
    return hologram;
  }

  private createSecurityBeacon(THREE: ThreeModule): import('three').Group {
    const beacon = new THREE.Group();
    const base = new THREE.Mesh(
      new THREE.CylinderGeometry(0.35, 0.48, 0.44, 8),
      new THREE.MeshStandardMaterial({ color: 0x172554, metalness: 0.55, roughness: 0.32 })
    );
    beacon.add(base);
    const light = new THREE.Mesh(
      new THREE.ConeGeometry(0.28, 1.4, 10, 1, true),
      new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.24, side: THREE.DoubleSide })
    );
    light.position.y = 0.9;
    beacon.add(light);
    return beacon;
  }

  private createParticles(THREE: ThreeModule): import('three').Points {
    const count = this.quality === 'full' ? 90 : this.quality === 'balanced' ? 54 : 28;
    const positions = new Float32Array(count * 3);
    for (let index = 0; index < count; index += 1) {
      positions[index * 3] = (Math.random() - 0.5) * 12;
      positions[index * 3 + 1] = 0.6 + Math.random() * 5.2;
      positions[index * 3 + 2] = (Math.random() - 0.5) * 9;
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    return new THREE.Points(
      geometry,
      new THREE.PointsMaterial({ color: 0x93c5fd, size: 0.045, transparent: true, opacity: 0.62, sizeAttenuation: true })
    );
  }

  private setupLights(THREE: ThreeModule): void {
    if (!this.scene) return;
    this.scene.add(new THREE.HemisphereLight(0xcffafe, 0x0b2554, 2.1));
    const key = new THREE.DirectionalLight(0xffffff, 2.5);
    key.position.set(5, 9, 7);
    key.castShadow = this.quality === 'full';
    if (key.shadow) {
      key.shadow.mapSize.set(1024, 1024);
      key.shadow.camera.near = 1;
      key.shadow.camera.far = 28;
    }
    this.scene.add(key);
    const rim = new THREE.PointLight(0x22d3ee, 14, 16, 2);
    rim.position.set(-4, 3.6, 2.5);
    this.scene.add(rim);
    const purple = new THREE.PointLight(0x8b5cf6, 9, 12, 2);
    purple.position.set(4, 2.4, -2.5);
    this.scene.add(purple);
  }

  private setupObservers(): void {
    const host = this.hostRef.nativeElement;
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(host);
    this.intersectionObserver = new IntersectionObserver(entries => {
      this.inViewport = entries[0]?.isIntersecting ?? true;
      this.syncLoopState();
    }, { threshold: 0.02 });
    this.intersectionObserver.observe(host);
    this.canvasRef.nativeElement.addEventListener('webglcontextlost', this.onContextLost, { passive: false });
    document.addEventListener('visibilitychange', this.syncLoopState);
  }

  private setupPointerMotion(): void {
    if (!this.camera || this.preferences.coarsePointer()) return;
    this.cameraXTo = gsap.quickTo(this.camera.position, 'x', { duration: 0.75, ease: 'power3.out' });
    this.cameraYTo = gsap.quickTo(this.camera.position, 'y', { duration: 0.75, ease: 'power3.out' });
    window.addEventListener('pointermove', this.onPointerMove, { passive: true });
    window.addEventListener('pointerleave', this.onPointerLeave, { passive: true });
  }

  private onPointerMove = (event: PointerEvent): void => {
    if (!this.camera || !this.pointer || !this.raycaster || !this.preferences.allowContinuousMotion()) return;
    const x = event.clientX / window.innerWidth * 2 - 1;
    const y = -(event.clientY / window.innerHeight) * 2 + 1;
    this.pointer.set(x, y);
    this.cameraXTo?.(7.6 + x * 0.42);
    this.cameraYTo?.(5.3 + y * 0.26);

    this.raycaster.setFromCamera(this.pointer, this.camera);
    const hit = this.raycaster.intersectObjects(this.interactiveObjects, true)[0]?.object ?? null;
    const interactiveRoot = hit ? this.findInteractiveRoot(hit) : null;
    if (interactiveRoot === this.hoveredObject) return;

    if (this.hoveredObject) gsap.to(this.hoveredObject.scale, { x: 1, y: 1, z: 1, duration: 0.28, ease: 'power2.out' });
    this.hoveredObject = interactiveRoot;
    if (interactiveRoot) gsap.to(interactiveRoot.scale, { x: 1.08, y: 1.08, z: 1.08, duration: 0.3, ease: 'back.out(1.8)' });
  };

  private onPointerLeave = (): void => {
    this.pointer?.set(0, 0);
    this.cameraXTo?.(7.6);
    this.cameraYTo?.(5.3);
    if (this.hoveredObject) gsap.to(this.hoveredObject.scale, { x: 1, y: 1, z: 1, duration: 0.25 });
    this.hoveredObject = null;
  };

  private findInteractiveRoot(object: import('three').Object3D): import('three').Object3D | null {
    let current: import('three').Object3D | null = object;
    while (current) {
      if (this.interactiveObjects.includes(current)) return current;
      current = current.parent;
    }
    return null;
  }

  private applyMode(animated: boolean): void {
    if (!this.camera || !this.world || !this.hologram || !this.securityBeacon) return;
    this.storyTimeline?.kill();
    const duration = animated && !this.preferences.reduceMotion() ? 0.75 : 0;
    const isOtp = this.mode === 'OTP_REGISTER' || this.mode === 'OTP_FORGOT';
    const isSignup = this.mode === 'SIGNUP';
    const isForgot = this.mode === 'FORGOT' || this.mode === 'OTP_FORGOT';
    const timeline = gsap.timeline({ defaults: { duration, ease: 'power3.inOut', overwrite: 'auto' } });
    this.storyTimeline = timeline;

    timeline.to(this.world.rotation, { y: isOtp ? 0.18 : isForgot ? -0.28 : isSignup ? 0.06 : -0.12 }, 0);
    timeline.to(this.world.position, { x: this.variant === 'auth' ? (isOtp ? -0.65 : -1.35) : 0 }, 0);
    timeline.to(this.camera.position, { z: isOtp ? 9.6 : 10.8, y: isOtp ? 4.9 : 5.3 }, 0);
    timeline.to(this.hologram.scale, { x: isOtp ? 1.65 : 1, y: isOtp ? 1.65 : 1, z: isOtp ? 1.65 : 1 }, 0);
    timeline.to(this.securityBeacon.scale, { x: isForgot ? 1 : 0.001, y: isForgot ? 1 : 0.001, z: isForgot ? 1 : 0.001 }, 0);
    if (this.extraCharacter) {
      timeline.to(this.extraCharacter.scale, { x: isSignup ? 1 : 0.001, y: isSignup ? 1 : 0.001, z: isSignup ? 1 : 0.001, ease: 'back.out(1.5)' }, 0.08);
    }

    this.taskBlocks.forEach((task, index) => {
      const home = task.userData['home'] as import('three').Vector3;
      const angle = index / this.taskBlocks.length * Math.PI * 2;
      const target = isOtp
        ? { x: 1.45 + Math.cos(angle) * 1.15, y: 2.1 + Math.sin(angle) * 1.15, z: 1.15 }
        : { x: home.x, y: home.y, z: home.z };
      timeline.to(task.position, target, 0);
    });
  }

  private renderFrame(): void {
    if (!this.renderer || !this.scene || !this.camera || !this.clock || !this.inViewport || this.preferences.documentHidden()) return;
    const time = this.clock.getElapsedTime();
    const motionFactor = this.preferences.allowContinuousMotion() ? 1 : 0;

    this.characters.forEach(character => {
      const phase = Number(character.userData['phase'] ?? 0);
      const baseY = Number(character.userData['baseY'] ?? character.position.y);
      if (character.scale.x > 0.01) character.position.y = baseY + Math.sin(time * 1.35 + phase) * 0.025 * motionFactor;
      const leftArm = character.userData['leftArm'] as import('three').Group;
      const rightArm = character.userData['rightArm'] as import('three').Group;
      const head = character.userData['headMesh'] as import('three').Mesh;
      if (leftArm) leftArm.rotation.x = Math.sin(time * 1.15 + phase) * 0.12 * motionFactor;
      if (rightArm) rightArm.rotation.x = Math.sin(time * 1.15 + phase + Math.PI) * 0.12 * motionFactor;
      if (head) head.rotation.y = (Math.sin(time * 0.62 + phase) * 0.12 + (this.pointer?.x ?? 0) * 0.08) * motionFactor;
    });

    if (this.robot) {
      this.robot.position.y = 1.18 + Math.sin(time * 1.8) * 0.09 * motionFactor;
      this.robot.rotation.y = (Math.sin(time * 0.72) * 0.12 + (this.pointer?.x ?? 0) * 0.1) * motionFactor;
      const ring = this.robot.userData['hoverRing'] as import('three').Object3D;
      if (ring) ring.rotation.z = time * 1.5 * motionFactor;
    }
    if (this.drone) {
      this.drone.position.x = -1.4 + Math.sin(time * 0.48) * 1.15 * motionFactor;
      this.drone.position.z = -0.8 + Math.cos(time * 0.48) * 0.55 * motionFactor;
      this.drone.position.y = 3.45 + Math.sin(time * 1.9) * 0.12 * motionFactor;
      this.drone.rotation.y = -time * 0.28 * motionFactor;
    }
    if (this.hologram) {
      this.hologram.children.forEach((child, index) => {
        child.rotation.y += (0.0025 + index * 0.001) * motionFactor;
        child.rotation.z += (index % 2 ? -0.002 : 0.002) * motionFactor;
      });
    }
    if (this.particles) this.particles.rotation.y = time * 0.018 * motionFactor;
    this.world?.children.forEach(child => {
      if (child.userData['cloud']) child.position.x += Math.sin(time * 0.12 + Number(child.userData['phase'])) * 0.0008 * motionFactor;
    });
    this.taskBlocks.forEach((task, index) => {
      task.rotation.z = Math.sin(time * 1.1 + index) * 0.025 * motionFactor;
    });

    this.camera.lookAt(this.variant === 'auth' ? -0.55 : 0, 1.25, 0);
    this.renderer.render(this.scene, this.camera);
  }

  private resize(): void {
    if (!this.renderer || !this.camera) return;
    const host = this.hostRef.nativeElement;
    const width = Math.max(host.clientWidth, 1);
    const height = Math.max(host.clientHeight, 1);
    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    this.camera.fov = width / height < 1 ? 43 : 34;
    this.camera.updateProjectionMatrix();
  }

  private syncLoopState = (): void => {
    if (!this.renderer) return;
    const shouldRun = this.inViewport && !document.hidden && !this.destroyed;
    if (shouldRun) {
      this.clock?.start();
      this.renderer.setAnimationLoop(this.renderLoop);
    } else {
      this.renderer.setAnimationLoop(null);
      this.clock?.stop();
    }
  };

  private renderLoop = (): void => this.renderFrame();

  private onContextLost = (event: Event): void => {
    event.preventDefault();
    this.fallback.set(true);
    this.renderer?.setAnimationLoop(null);
  };

  private disposeScene(): void {
    this.scene?.traverse(object => {
      const mesh = object as import('three').Mesh;
      mesh.geometry?.dispose();
      const materials = Array.isArray(mesh.material) ? mesh.material : mesh.material ? [mesh.material] : [];
      materials.forEach(material => {
        const record = material as import('three').Material & Record<string, unknown>;
        Object.values(record).forEach(value => {
          if (value && typeof value === 'object' && 'isTexture' in value) {
            (value as import('three').Texture).dispose();
          }
        });
        material.dispose();
      });
    });
    this.scene?.clear();
    this.renderer?.dispose();
    this.renderer?.forceContextLoss();
    this.renderer = null;
    this.scene = null;
    this.camera = null;
    this.THREE = null;
  }

  private syncLabel(): void {
    const labels: Record<NeoCampusMode, string> = {
      LOGIN: 'Scrum Lab đang hoạt động',
      SIGNUP: 'Đang chuẩn bị vị trí thành viên mới',
      OTP_REGISTER: 'AI đang xác thực mã bảo mật',
      FORGOT: 'Drone đang mở tuyến bảo mật',
      OTP_FORGOT: 'Đang khôi phục quyền truy cập',
      COMMAND: 'Trung tâm dự án đang đồng bộ',
      AI_LAB: 'AI Lab đang giám sát mô hình'
    };
    this.label.set(labels[this.mode]);
  }
}
