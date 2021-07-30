
const fs = require("fs");
const del = require('del');
const gulp = require('gulp');
const sourcemap = require('gulp-sourcemaps');
const webserver = require('gulp-connect');

const abundle = require('aurelia-bundler').bundle;

const transpiler = require('gulp-typescript');
const tsconfig = transpiler.createProject('tsconfig.json');

gulp.task('clear-all', async () => {
    await del(["./dist/**/*"]);
});

gulp.task('copy-lib', () => {
    return gulp.src([
        "./jspm_packages/**/*"
        ])
        .pipe(gulp.dest("./dist/jspm_packages/"));
});

gulp.task('copy-index', () => {
    return gulp.src([
        "./index.html",
        ])
        .pipe(gulp.dest("./dist/"));
});

gulp.task('copy-module-config', () => {
    return gulp.src(["./config.js"]).pipe(gulp.dest("./dist/"));
});

gulp.task('backup-module-config', () => {
    return gulp.src(["./config.js"]).pipe(gulp.dest("./tmp/"));
});

gulp.task('restore-module-config', () => {
    return gulp.src(["./tmp/config.js"]).pipe(gulp.dest("./"));
});

gulp.task('copy-template', () => {
    return gulp.src([
        "./src/**/*.html",
        "./src/**/*.css"
        ])
        .pipe(gulp.dest("./dist/"));
});

gulp.task('copy-resource', () => {
    return gulp.src([
        "./res/**/*"
        ])
        .pipe(gulp.dest("./dist/"));
});

gulp.task("transpile-ts", () => {
    return gulp.src([
        "./src/**/*.ts"
    ])
    .pipe(sourcemap.init({ loadMaps: true }))
    .pipe(tsconfig()).js
    .pipe(sourcemap.write("./", {includeContent: false, sourceRoot: '../src'}))
    .pipe(gulp.dest("./dist/"));
});

gulp.task('create-fake-bundle', async () => {
    fs.writeFileSync('./dist/bundle-app.js', "console.log('fake bundle-app is loaded');", 'utf8');
    fs.writeFileSync('./dist/bundle-vendor.js', "console.log('fake bundle-vendor is loaded');", 'utf8');
});

gulp.task("bundle", async() => {
    let bundleConfig = JSON.parse(fs.readFileSync('./bundle.json', 'utf8'));
    return abundle(bundleConfig);
});

gulp.task("apply-config", async () => {
    let appConfig = JSON.parse(fs.readFileSync('./app-config.json', 'utf8'));
    let configCode = "window.appConfig = JSON.parse('" + JSON.stringify(appConfig) + "');";
    fs.writeFileSync('./dist/js/app-config.js', configCode, 'utf8');
});

gulp.task("watch", () => {
    return gulp.watch(["./index.html", "./app-config.json", "./src/**/*", "./res/**/*"], gulp.series("build-main"));
});

gulp.task('copy-module-config', function () {
    return gulp.src(["./config.js"]).pipe(gulp.dest("./dist/"));
});

gulp.task('backup-module-config', function () {
    return gulp.src(["./config.js"]).pipe(gulp.dest("./tmp/"));
});

gulp.task('restore-module-config', function () {
    return gulp.src(["./tmp/config.js"]).pipe(gulp.dest("./"));
});

gulp.task("build-main", gulp.series(
             'copy-index',
             'copy-template',
             'copy-resource',
             'transpile-ts',
             'apply-config')
);

gulp.task("build-and-watch", gulp.series(
            'clear-all',
            ['copy-lib', 'copy-index', 'copy-module-config'],
             'copy-template',
             'copy-resource',
             'transpile-ts',
             'apply-config',
             'create-fake-bundle',
             'watch')
);

gulp.task('clean-up', async() => {
    await del(["dist/*.js.map"]);
    await del(["dist/*.js", "!dist/config.js", "!dist/bundle-app.js", "!dist/bundle-vendor.js"]);
    await del(["dist/*.html", "!dist/index.html"]);
    await del(["tmp/**/*"]);
    await del(["dist/jspm_packages/github/**/*"]);
    //await del(["dist/jspm_packages/npm/**/*"]);
    await del(["dist/jspm_packages/npm/**/*", 
                "!dist/jspm_packages/npm/aurelia-dialog*", "!dist/jspm_packages/npm/aurelia-dialog*/*.js" // ...
             ]);
});

gulp.task("release", gulp.series(
    'clear-all',
    ['copy-lib', 'copy-module-config'],
     'copy-template',
     'copy-resource',
     'transpile-ts',
     'backup-module-config',
     'bundle',
     'copy-module-config',
     'restore-module-config',
     'apply-config',
     'copy-index',
     'clean-up')
);

gulp.task('start', async () => {
    webserver.server({
        name: 'WebServer',
        root: '',
        port: 9090,
        livereload: true
    });
});

gulp.task('default', gulp.series('build-and-watch'));
